using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Blocks;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Models.Elements;
using SlackBotManager.API.Models.ElementStates;
using SlackBotManager.API.Models.Events;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Models.Repositories;
using SlackBotManager.API.Models.Surfaces;
using SlackBotManager.API.Models.Views;

namespace SlackBotManager.API.Services;

public class HomeTabInvocation : IEventInvocation, IBlockActionsInvocation, IViewSubmissionInvocation
{
    private readonly ISettingRepository _settingRepository;
    private readonly CreatePullRequestInvocation _createPullRequestInvocation;

    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; } = [];
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> BlockActionsBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task>> ViewSubmissionBindings { get; } = [];

    public HomeTabInvocation(ISettingRepository settingRepository, CreatePullRequestInvocation createPullRequestInvocation)
    {
        EventBindings.Add("app_home_opened", SendHomeTab);

        BlockActionsBindings.Add(("channel_to_post", "select"), SetChannelToPost);
        BlockActionsBindings.Add(("app_admins", "select"), SetApplicationAdmins);
        BlockActionsBindings.Add(("pull_request", "create"), CreatePullRequest);
        BlockActionsBindings.Add(("creation_message", "remove"), RemoveCreationMessage);
        BlockActionsBindings.Add(("branches", "edit"), EditBranches);
        BlockActionsBindings.Add(("tags", "edit"), EditTags);

        ViewSubmissionBindings.Add("edit_branches", SubmitEditBranches);
        ViewSubmissionBindings.Add("edit_tags", SubmitEditTags);

        _settingRepository = settingRepository;
        _createPullRequestInvocation = createPullRequestInvocation;
    }

    private async Task SendHomeTab(SlackClient slackClient, EventPayload payload)
    {
        var authorization = payload.Authorizations.First();
        var setting = await _settingRepository.Find(authorization.EnterpriseId, authorization.TeamId, authorization.UserId, authorization.IsEnterpriseInstall);
        await slackClient.ViewPublish(payload.Event.User!, await BuildHomeView(slackClient, payload.Event.User!, setting));
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
                        Filter = new() { Include = [Filter.ConversationType.DirectMessages], ExcludeBotUsers = true }
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

            blocks.AddRange(
            [
                new DividerBlock(),
                new SectionBlock(new MarkdownText($"Branches: {string.Join(", ", setting.Branches.Select(b => $"`{b}`"))}"))
                {
                    BlockId = "branches",
                    Accessory = new Button("Edit branches")
                    {
                        ActionId = "edit"
                    }
                },
                new SectionBlock(new MarkdownText($"Tags: {string.Join(", ", setting.Tags.Select(b => $"`{b}`"))}"))
                {
                    BlockId = "tags",
                    Accessory = new Button("Edit tags")
                    {
                        ActionId = "edit"
                    }
                },
            ]);
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
            CommandText = "/create_pull_request",
            EnterpriseId = payload.Enterprise?.Id,
            TeamId = payload.Team?.Id,
            IsEnterpriseInstall = payload.IsEnterpriseInstall,
            UserId = payload.User.Id,
            TriggerId = payload.TriggerId
        };
        var result = await _createPullRequestInvocation.CommandBindings[command.CommandText].Invoke(slackClient, command);

        if (!result.IsSuccesful)
        {
            var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting, ("pull_request", result.Error!)));
        }
    }

    private async Task SetChannelToPost(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall) ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.CreatePullRequestChannelId = ((SelectPublicChannelState)payload.View.State.Values["channel_to_post"]["select"]).SelectedChannel;
        await _settingRepository.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
    }

    private async Task SetApplicationAdmins(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall) ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.ApplicationAdminUsers = ((MultiSelectConversationsState)payload.View.State.Values["app_admins"]["select"]).SelectedConversations;
        await _settingRepository.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
    }

    private async Task RemoveCreationMessage(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

        if (!string.IsNullOrEmpty(setting?.CurrentPullRequestReview?.MessageTimestamp))
        {
            await slackClient.ChatDeleteMessage(setting.CreatePullRequestChannelId!, setting.CurrentPullRequestReview.MessageTimestamp);
            setting.CurrentPullRequestReview = null;
            await _settingRepository.Save(setting);
        }
    }

    private async Task EditBranches(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of branches separated by space", new PlainTextInput() { ActionId = "input", InitialValue = string.Join(" ", setting.Branches)})
            {
                BlockId = "branches"
            }
        ];

        var editBranchesModal = new ModalView("Edit branches", blocks) { Submit = new("Save"), CallbackId = "edit_branches" };
        await slackClient.ViewOpen(payload.TriggerId, editBranchesModal);
    }

    private async Task SubmitEditBranches(SlackClient slackClient, ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["branches"]["input"] is PlainTextInputState plainTextInputState)
        {
            var branches = plainTextInputState.Value.Split(' ').Distinct();
            var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

            if (branches.Count() != setting.Branches.Count() || branches.Intersect(setting.Branches).Count() != setting.Branches.Count())
            {
                setting.Branches = branches;
                await _settingRepository.Save(setting);
                await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
            }
        }
    }

    private async Task EditTags(SlackClient slackClient, BlockActionsPayload payload)
    {
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of tags separated by space", new PlainTextInput() { ActionId = "input", InitialValue = string.Join(" ", setting.Tags)})
            {
                BlockId = "tags"
            }
        ];

        var editBranchesModal = new ModalView("Edit tags", blocks) { Submit = new("Save"), CallbackId = "edit_tags" };
        await slackClient.ViewOpen(payload.TriggerId, editBranchesModal);
    }

    private async Task SubmitEditTags(SlackClient slackClient, ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["tags"]["input"] is PlainTextInputState plainTextInputState)
        {
            var tags = plainTextInputState.Value.Split(' ').Distinct();
            var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);

            if (tags.Count() != setting.Tags.Count() || tags.Intersect(setting.Tags).Count() != setting.Tags.Count())
            {
                setting.Tags = tags;
                await _settingRepository.Save(setting);
                await slackClient.ViewPublish(payload.User.Id, await BuildHomeView(slackClient, payload.User.Id, setting));
            }
        }
    }
}
