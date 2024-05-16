using SlackBotManager.API.Invocations;
using SlackBotManager.Slack.Blocks;
using SlackBotManager.Slack.Commands;
using SlackBotManager.Slack.Elements;
using SlackBotManager.Slack.ElementStates;
using SlackBotManager.Slack.Events;
using SlackBotManager.Slack.Payloads;
using SlackBotManager.Slack.Views;
using SlackBotManager.Slack;
using SlackBotManager.Persistence;
using SlackBotManager.Persistence.Models;

namespace SlackBotManager.API.Services;

public class HomeTabInvocation : IEventInvocation, IBlockActionsInvocation, IViewSubmissionInvocation
{
    private readonly ISettingStore _settingStore;
    private readonly QueueStateManager _queueStateManager;
    private readonly ICommandInvocation _createPullRequestInvocation;

    public Dictionary<string, Func<SlackClient, EventPayload, Task>> EventBindings { get; } = [];
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; } = [];

    public HomeTabInvocation(ISettingStore settingStore, QueueStateManager queueStateManager, ICommandInvocation createPullRequestInvocation)
    {
        _settingStore = settingStore;
        _queueStateManager = queueStateManager;
        _createPullRequestInvocation = createPullRequestInvocation;

        EventBindings.Add("app_home_opened", SendHomeTab);

        BlockActionsBindings.Add(("channel_to_post", "select"), SetChannelToPost);
        BlockActionsBindings.Add(("app_admins", "select"), SetApplicationAdmins);
        BlockActionsBindings.Add(("pull_request", "create"), CreatePullRequest);
        BlockActionsBindings.Add(("creation_message", "remove"), RemoveCreationMessage);
        BlockActionsBindings.Add(("branches", "edit"), EditBranches);
        BlockActionsBindings.Add(("tags", "edit"), EditTags);

        ViewSubmissionBindings.Add("edit_branches", SubmitEditBranches);
        ViewSubmissionBindings.Add("edit_tags", SubmitEditTags);
    }

    private async Task SendHomeTab(SlackClient client, EventPayload payload)
    {
        await client.ViewPublish(payload.Event.User!, await BuildHomeView(client, payload.Event.User!));
    }

    private async Task<HomeView> BuildHomeView(SlackClient client,
                                               string userId,
                                               params (string BlockId, string Message)[] validationMessages)
    {
        var userInfoResult = await client.UserInfo(userId);
        var setting = await _settingStore.Find() ?? new();

        var isAppAdmin = userInfoResult.Value.User.IsAdmin || (setting.ApplicationAdminUsers?.Contains(userId) ?? false);

        List<IBlock> blocks =
        [
            new SectionBlock("Button to start the process of creating the pull request.")
            {
                BlockId = "pull_request",
                Accessory = new Button("Create Pull Request") { ActionId = "create" }
            },
        ];

        if (isAppAdmin)
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
                        InitialChannel = setting.CreatePullRequestChannelId
                    }
                },
                new SectionBlock("Select users who can administrate this application in addition to workspace admin(s)")
                {
                    BlockId = "app_admins",
                    Accessory = new MultiSelectConversations()
                    {
                        Placeholder = new("Select application admins"),
                        ActionId = "select",
                        InitialConversations = setting.ApplicationAdminUsers,
                        Filter = new() { Include = [Filter.ConversationType.DirectMessages], ExcludeBotUsers = true }
                    }
                },                
            ]);

            if (!string.IsNullOrEmpty((await _queueStateManager.GetReviewInCreation())?.MessageTimestamp))
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
                new SectionBlock(new MarkdownText($"Branches: {string.Join(", ", setting.Branches.Select(b => $"`{b}`") ?? [])}"))
                {
                    BlockId = "branches",
                    Accessory = new Button("Edit branches")
                    {
                        ActionId = "edit"
                    }
                },
                new SectionBlock(new MarkdownText($"Tags: {string.Join(", ", setting.Tags.Select(b => $"`{b}`") ?? [])}"))
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

    private async Task<IRequestResult> CreatePullRequest(SlackClient client, BlockActionsPayload payload)
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
        var result = await _createPullRequestInvocation.CommandBindings[command.CommandText].Invoke(client, command);

        if (!result.IsSuccesful)
            await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id, ("pull_request", result.Error!)));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SetChannelToPost(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find() ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.CreatePullRequestChannelId = ((SelectPublicChannelState)payload.View.State.Values["channel_to_post"]["select"]).SelectedChannel;
        await _settingStore.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SetApplicationAdmins(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find() ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.ApplicationAdminUsers = ((MultiSelectConversationsState)payload.View.State.Values["app_admins"]["select"]).SelectedConversations;
        await _settingStore.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> RemoveCreationMessage(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find();
        var reviewInCreation = await _queueStateManager.GetReviewInCreation();

        if (reviewInCreation != null)
        {
            await _queueStateManager.CancelCreation(payload.User.Id, true);
            await client.ChatDeleteMessage(setting.CreatePullRequestChannelId!, reviewInCreation.MessageTimestamp!);
            await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));
        }

        return RequestResult.Success();
    }

    private async Task<IRequestResult> EditBranches(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find();

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of branches separated by space, the first one will be selected by default on creation reviews",
                           new PlainTextInput() { ActionId = "input", InitialValue = string.Join(" ", setting.Branches)})
            {
                BlockId = "branches"
            }
        ];

        var editBranchesModal = new ModalView("Edit branches", blocks) { Submit = new("Save"), CallbackId = "edit_branches" };
        await client.ViewOpen(payload.TriggerId, editBranchesModal);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitEditBranches(SlackClient client, ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["branches"]["input"] is PlainTextInputState plainTextInputState)
        {
            var branches = plainTextInputState.Value.Split(' ').Distinct();
            var setting = await _settingStore.Find();

            if (branches.Count() != setting.Branches.Count() || branches.Intersect(setting.Branches).Count() != setting.Branches.Count())
            {
                setting.Branches = branches;
                await _settingStore.Save(setting);
                await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));
            }
        }

        return RequestResult.Success();
    }

    private async Task<IRequestResult> EditTags(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find();

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of tags separated by space", new PlainTextInput() { ActionId = "input", InitialValue = string.Join(" ", setting.Tags)})
            {
                BlockId = "tags"
            }
        ];

        var editBranchesModal = new ModalView("Edit tags", blocks) { Submit = new("Save"), CallbackId = "edit_tags" };
        await client.ViewOpen(payload.TriggerId, editBranchesModal);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitEditTags(SlackClient client, ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["tags"]["input"] is PlainTextInputState plainTextInputState)
        {
            var tags = plainTextInputState.Value.Split(' ').Distinct();
            var setting = await _settingStore.Find();

            if (tags.Count() != setting.Tags.Count() || tags.Intersect(setting.Tags).Count() != setting.Tags.Count())
            {
                setting.Tags = tags;
                await _settingStore.Save(setting);
                await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));
            }
        }

        return RequestResult.Success();
    }
}
