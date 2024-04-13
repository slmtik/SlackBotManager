using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Blocks;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Models.Elements;
using SlackBotManager.API.Models.ElementStates;
using SlackBotManager.API.Models.Events;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Models.Repositories;
using SlackBotManager.API.Models.Views;

namespace SlackBotManager.API.Services;

public class HomeTabInvocation : IEventInvocation, IBlockActionsInvocation
{
    private readonly ISettingRepository _settingStore;
    private readonly CreatePullRequestInvocation _createPullRequestInvocation;

    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; } = [];
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> BlockActionsBindings { get; } = [];

    public HomeTabInvocation(ISettingRepository settingStore, CreatePullRequestInvocation createPullRequestInvocation)
    {
        EventBindings.Add("app_home_opened", SendHomeTab);

        BlockActionsBindings.Add(("channel_to_post", "select"), SetChannelToPost);
        BlockActionsBindings.Add(("app_admins", "select"), SetApplicationAdmins);
        BlockActionsBindings.Add(("pull_request", "create"), CreatePullRequest);
        BlockActionsBindings.Add(("creation_message", "remove"), RemoveCreationMessage);

        _settingStore = settingStore;
        _createPullRequestInvocation = createPullRequestInvocation;
    }

    private async Task SendHomeTab(SlackClient slackClient, EventPayload eventPayload)
    {
        var authorization = eventPayload.Authorizations!.First();
        var setting = _settingStore.Find(authorization.EnterpriseId, authorization.TeamId, authorization.UserId, authorization.IsEnterpriseInstall);
        await slackClient.ViewPublish(eventPayload.Event!.User!, await BuildHomeView(slackClient, eventPayload.Event!.User!, setting));
    }

    private static async Task<HomeView> BuildHomeView(SlackClient slackClient,
                                               string userId,
                                               Setting? setting,
                                               params (string BlockId, string Message)[] validationMessages)
    {
        var userInfoResult = await slackClient.UserInfo(userId);

        List<IBlock> blocks =
        [
            new SectionBlock("Button to start the process of creating the pull request.")
            {
                BlockId = "pull_request",
                Accessory = new Button("Create Pull Request") { ActionId = "create" }
            }
        ];

        if (userInfoResult.Value.User.IsAdmin || (setting?.ApplicationAdminUsers?.Contains(userId) ?? false))
        {
            blocks.AddRange(
            [
                new DividerBlock(),
                new HeaderBlock("Administrator settings:"),
                new SectionBlock("Channel to post messages")
                {
                    BlockId = "channel_to_post",
                    Accessory = new SelectPublicChannel()
                    {
                        Placeholder = new("Select a channel"),
                        ActionId = "select",
                        InitialChannel = setting?.CreatePullRequestChannelId
                    }
                },
                new SectionBlock("Select users who can administrate this application in addition to workspace admin(s)")
                {
                    BlockId = "app_admins",
                    Accessory = new MultiSelectConversations()
                    {
                        Placeholder = new("Select application admins"),
                        ActionId = "select",
                        InitialConversations = setting?.ApplicationAdminUsers,
                        Filter = new() { ConversationTypes = [ConversationType.DirectMessages], ExcludeBotUsers = true }
                    }
                },                
            ]);

            if (!string.IsNullOrEmpty(setting?.CurrentPullRequestReview?.MessageTimestamp))
            {
                blocks.AddRange(
                [
                    new DividerBlock(),
                    new SectionBlock("In case creation message stucked, you can remove it")
                    {
                        BlockId = "creation_message",
                        Accessory = new Button("Remove creation message")
                        {
                            ActionId = "remove",
                            Confirm = new("Remove the creation message", "You are going to remove the creation message. Please confirm", "Remove", "Cancel")
                        }
                    },
                ]);
            }
        }

        if (validationMessages != null)
        {
            foreach (var (BlockId, Message) in validationMessages)
            {
                var blockToValidateIndex = blocks.FindIndex(b => b.BlockId == BlockId) + 1;
                blocks.Insert(blockToValidateIndex, new ContextBlock([new MarkdownText($":warning: {Message}")]) { BlockId = $"validation_{BlockId}" });
            }
        }

        return new(blocks);
    }

    private async Task CreatePullRequest(SlackClient slackClient, BlockActionsPayload payload)
    {
        var command = new Command()
        {
            EnterpriseId = payload.Enterprise?.Id,
            TeamId = payload.Team?.Id,
            IsEnterpriseInstall = payload.IsEnterpriseInstall,
            UserId = payload.User.Id,
            TriggerId = payload.TriggerId
        };
        var result = await _createPullRequestInvocation.CommandBindings["/create_pull_request"].Invoke(slackClient, command);

        if (!result.IsSuccesful)
        {
            var setting = _settingStore.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting, ("pull_request", result.Error!)));
        }
    }

    private async Task SetChannelToPost(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = _settingStore.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall) ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.CreatePullRequestChannelId = ((SelectPublicChannelState)payload.View.State.Values["channel_to_post"]["select"]).SelectedChannel;
        _settingStore.Save(setting);

        if (payload.View!.Blocks!.Any(b => b.BlockId!.StartsWith("validation")))
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
    }

    private async Task SetApplicationAdmins(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = _settingStore.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall) ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.ApplicationAdminUsers = ((MultiSelectConversationsState)payload.View.State.Values["app_admins"]["select"]).SelectedConversations;
        _settingStore.Save(setting);

        if (payload.View!.Blocks!.Any(b => b.BlockId!.StartsWith("validation")))
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
    }

    private async Task RemoveCreationMessage(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = _settingStore.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

        if (!string.IsNullOrEmpty(setting?.CurrentPullRequestReview?.MessageTimestamp))
        {
            await slackClient.ChatDeleteMessage(setting.CreatePullRequestChannelId!, setting.CurrentPullRequestReview.MessageTimestamp);
            setting.CurrentPullRequestReview = null;
            _settingStore.Save(setting);
        }
    }

}
