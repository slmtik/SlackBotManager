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
using Slack.Models.Elements;
using Persistence.Models;

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
        BlockActionsBindings.Add(("pull_requests_creation", "allow"), UpdateCreationAllowance);
        BlockActionsBindings.Add(("pull_requests_creation", "disallow"), UpdateCreationAllowance);
        BlockActionsBindings.Add(("reminder", "settings"), SetupReminder);
        BlockActionsBindings.Add(("reminder", "enable"), EnableReminder);

        ViewSubmissionBindings.Add("edit_branches", SubmitEditBranches);
        ViewSubmissionBindings.Add("edit_tags", SubmitEditTags);
        ViewSubmissionBindings.Add("configure_reminder", SubmitConfigureReminder);
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
        var setting = await _settingStore.Find();
        if (setting is null)
        {
            setting = new();
            await _settingStore.Save(setting);
        }

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

            string allowancePrefix;
            if (await _queueStateManager.IsCreationAllowed())
                allowancePrefix = "Disallow";
            else
                allowancePrefix = "Allow";

            blocks.AddRange(
                [
                    new DividerBlock(),
                    new SectionBlock("Manage the allowance of creation pull requests")
                    {
                        BlockId = "pull_requests_creation",
                        Accessory = new Button($"{allowancePrefix} creation")
                        {
                            ActionId = allowancePrefix.ToLower(),
                            Confirm = new($"{allowancePrefix}ing to create pull requests",
                                          $"You are going to {allowancePrefix.ToLower()} the creation of pull requests. Please confirm",
                                          allowancePrefix,
                                          "Cancel")
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

            blocks.AddRange(
            [
                new DividerBlock(),
                new SectionBlock(new MarkdownText("Reminder settings"))
                {
                    BlockId = "reminder",
                    Accessory = new Button("Setup reminder logic")
                    {
                        ActionId = "settings"
                    }
                }
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

    private async Task<IRequestResult> UpdateCreationAllowance(SlackClient client, BlockActionsPayload payload)
    {
        var actionId = payload.Actions.FirstOrDefault()?.ActionId;
        bool allowCreatePullRequests = actionId == "allow";

        string channelMessage, topicMessage;
        if (allowCreatePullRequests)
        {
            topicMessage = "Feel Free to Create Pull Requests";
            channelMessage = ":white_check_mark:Feel Free to Create Pull Requests:white_check_mark:";
        }
        else
        {
            topicMessage = "Do NOT Create Pull Request";
            channelMessage = ":x:Do NOT Create Pull Request:x:";
        }

        await _queueStateManager.UpdateCreationAllowance(allowCreatePullRequests);

        var setting = await _settingStore.Find();
        await client.ChatPostMessage(new(setting.CreatePullRequestChannelId, channelMessage));
        await client.ConversationsSetTopic(setting.CreatePullRequestChannelId, topicMessage);
        await client.ViewPublish(payload.User.Id, await BuildHomeView(client, payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SetupReminder(SlackClient client, BlockActionsPayload payload)
    {
        await client.ViewOpen(payload.TriggerId, await BuildConfigureReminderModal());
        return RequestResult.Success();
    }

    private async Task<ModalView> BuildConfigureReminderModal()
    {
        var setting = await _settingStore.Find();
        ReminderSetting reminderSetting = setting.ReminderSetting ?? new();

        List<IBlock> blocks =
        [
            new SectionBlock($"{(reminderSetting.Enabled ? "Disable" : "Enable")} reminder")
            {
                Accessory = new Button(reminderSetting.Enabled ? "Disable" : "Enable") { ActionId = "enable" },
                BlockId = "reminder"
            }
        ];

        if (reminderSetting.Enabled)
            blocks.AddRange([
                new InputBlock("Channel to use for reminding", new SelectPublicChannel() { ActionId = "select", InitialChannel = reminderSetting.RemindingChannelId })
                {
                    BlockId = "channel"
                },
                    new InputBlock("Enter reminder message template", new PlainTextInput()
                {
                    ActionId = "input",
                    InitialValue = reminderSetting.MessageTemplate
                })
                {
                    BlockId = "template"
                },
                new InputBlock("After what period in minutes to remind?", new NumberInput()
                {
                    ActionId = "input",
                    InitialValue = reminderSetting.TimeToRemindInMinutes.ToString(),
                    MinValue = "1"
                })
                {
                    BlockId = "minutes"
                },
                new InputBlock("Work day starts at (UTC):", new TimePicker() 
                { 
                    InitialTime = reminderSetting.WorkDayStart,
                    ActionId = "select"
                })
                {
                    BlockId = "day_starts"
                },
                new InputBlock("Work day ends at (UTC):", new TimePicker() 
                { 
                    InitialTime = reminderSetting.WorkDayEnd,
                    ActionId = "select"
                })
                {
                    BlockId = "day_ends"
                }
            ]);

        var configureReminderModal = new ModalView("Configure reminder logic", blocks) { Submit = new("Save"), CallbackId = "configure_reminder" };
        return configureReminderModal;
    }

    private async Task<IRequestResult> EnableReminder(SlackClient client, BlockActionsPayload payload)
    {
        var setting = await _settingStore.Find() ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.ReminderSetting ??= new ReminderSetting();
        setting.ReminderSetting.Enabled = !setting.ReminderSetting.Enabled;

        await _settingStore.Save(setting);
        await client.ViewUpdate(payload.View.RootViewId, await BuildConfigureReminderModal());
        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitConfigureReminder(SlackClient client, ViewSubmissionPayload payload)
    {
        var setting = await _settingStore.Find() ??
            new Setting()
            {
                EnterpriseId = payload.Enterprise?.Id,
                TeamId = payload.Team?.Id,
                IsEnterpriseInstall = payload.IsEnterpriseInstall,
            };

        setting.ReminderSetting ??= new ReminderSetting();

        if (setting.ReminderSetting.Enabled)
        {
            setting.ReminderSetting.RemindingChannelId = ((SelectPublicChannelState)payload.View.State.Values["channel"].First().Value).SelectedChannel;
            setting.ReminderSetting.MessageTemplate = ((PlainTextInputState)payload.View.State.Values["template"].First().Value).Value;
            setting.ReminderSetting.TimeToRemindInMinutes = int.Parse(((NumberInputState)payload.View.State.Values["minutes"].First().Value).Value);
            setting.ReminderSetting.WorkDayStart = ((TimePickerState)payload.View.State.Values["day_starts"].First().Value).SelectedTime;
            setting.ReminderSetting.WorkDayEnd = ((TimePickerState)payload.View.State.Values["day_ends"].First().Value).SelectedTime;

            await _settingStore.Save(setting);
        }

        return RequestResult.Success();
    }

}
