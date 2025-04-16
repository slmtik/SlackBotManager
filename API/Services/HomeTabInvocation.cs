using Slack.Models.Elements;
using Persistence.Models;
using Persistence.Interfaces;
using Slack;
using Slack.Interfaces;
using Slack.Models.ElementStates;
using Slack.Models.Views;
using API.Interfaces.Invocations;
using Slack.Models.Payloads;
using Slack.Models.Events;
using Slack.Models.Blocks;
using Slack.Models.Commands;
using Core.ApiClient;
using API.Interfaces;
using System.Diagnostics;

namespace API.Services;

public class HomeTabInvocation : IEventInvocation, IBlockActionsInvocation, IViewSubmissionInvocation
{
    private readonly SlackClient _slackClient;
    private readonly ISettingStore _settingStore;
    private readonly QueueStateManager _queueStateManager;
    private readonly PullRequestInvocation _createPullRequestInvocation;
    private readonly IVersionStrategistResolver _versionStrategistResolver;

    public Dictionary<string, Func<EventPayload, Task<IRequestResult>>> EventBindings { get; } = [];
    public Dictionary<(string? BlockId, string? ActionId), Func<BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; } = [];
    public Dictionary<string, Func<ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; } = [];

    public HomeTabInvocation(SlackClient slackClient,
                             ISettingStore settingStore,
                             QueueStateManager queueStateManager,
                             PullRequestInvocation createPullRequestInvocation,
                             IVersionStrategistResolver versionStrategistResolver)
    {
        _slackClient = slackClient;
        _settingStore = settingStore;
        _queueStateManager = queueStateManager;
        _createPullRequestInvocation = createPullRequestInvocation;
        _versionStrategistResolver = versionStrategistResolver;

        EventBindings.Add("app_home_opened", SendHomeTab);

        BlockActionsBindings.Add(("channel_to_post", "select"), SetChannelToPost);
        BlockActionsBindings.Add(("app_admins", "select"), SetApplicationAdmins);
        BlockActionsBindings.Add(("pull_request", "create"), CreatePullRequest);
        BlockActionsBindings.Add(("creation_message", "remove"), RemoveCreationMessage);
        BlockActionsBindings.Add(("branches", "edit"), ManageBranches);
        BlockActionsBindings.Add(("tags", "edit"), ManageTags);
        BlockActionsBindings.Add(("pull_requests_creation", "allow"), UpdateCreationAllowance);
        BlockActionsBindings.Add(("pull_requests_creation", "disallow"), UpdateCreationAllowance);
        BlockActionsBindings.Add(("reminder", "settings"), SetupReminder);
        BlockActionsBindings.Add(("reminder", "enable"), EnableReminder);
        BlockActionsBindings.Add(("webhook", "settings"), SetupWebhook);
        BlockActionsBindings.Add((null, "select_version_strategy"), SelectVersionStrategy);

        ViewSubmissionBindings.Add("edit_branches", SubmitManageBranches);
        ViewSubmissionBindings.Add("edit_tags", SubmitEditTags);
        ViewSubmissionBindings.Add("configure_reminder", SubmitConfigureReminder);
        ViewSubmissionBindings.Add("configure_webhook", SubmitConfigureWebhook);
    }

    private async Task<IRequestResult> SendHomeTab(EventPayload payload)
    {
        var setting = await _settingStore.Find() ?? new();
        await _settingStore.Save(setting);
        return await _slackClient.ViewPublish(payload.Event.User!, await BuildHomeView(payload.Event.User!));
    }

    private async Task<HomeView> BuildHomeView(string userId,
                                               params (string BlockId, string Message)[] validationMessages)
    {
        var userInfoResult = await _slackClient.UserInfo(userId);
        Setting setting = (await _settingStore.Find())!;

        var isAppAdmin = userInfoResult.Value.User.IsAdmin || (setting.ApplicationAdminUsers?.Contains(userId) ?? false);

        List<IBlock> blocks =
        [
            new SectionBlock("Button to initiate the pull request creation process.")
            {
                BlockId = "pull_request",
                Accessory = new Button("Create Pull Request") { ActionId = "create" }
            },
        ];

        if (isAppAdmin)
        {
            blocks.AddRange([new DividerBlock(), new HeaderBlock("Administrator Settings")]);
            blocks.AddRange(CreateMainSettingsBlocks(setting));
            blocks.AddRange(await CreatePullRequestAllowanceBlocks());
            blocks.AddRange(await CreateRemoveCreationMessageBlocks());
            blocks.AddRange(CreateEditFieldsBlocks(setting));
            blocks.AddRange(CreateReminderSettingBlocks());
            blocks.AddRange(CreateWebhookSettingsBlocks());
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

    private static IList<IBlock> CreateWebhookSettingsBlocks()
    {
        return
        [
            new DividerBlock(),
            new SectionBlock(new PlainText("Webhook Settings"))
            {
                BlockId = "webhook",
                Accessory = new Button("Configure webhook")
                {
                    ActionId = "settings"
                }
            }
        ];
    }

    private static IList<IBlock> CreateReminderSettingBlocks()
    {
        return
        [
            new DividerBlock(),
            new SectionBlock(new PlainText("Reminder Settings"))
            {
                BlockId = "reminder",
                Accessory = new Button("Configure reminder")
                {
                    ActionId = "settings"
                }
            }
        ];
    }

    private static IList<IBlock> CreateEditFieldsBlocks(Setting setting)
    {
        return
        [
            new DividerBlock(),
            new SectionBlock(new MarkdownText($"Branches: {string.Join(", ", setting.Branches.Select(b => $"`{b}`") ?? [])}"))
            {
                BlockId = "branches",
                Accessory = new Button("Manage Branches")
                {
                    ActionId = "edit"
                }
            },
            new SectionBlock(new MarkdownText($"Tags: {string.Join(", ", setting.Tags.Select(b => $"`{b}`") ?? [])}"))
            {
                BlockId = "tags",
                Accessory = new Button("Manage Tags")
                {
                    ActionId = "edit"
                }
            },
        ];
    }

    private async Task<IList<IBlock>> CreateRemoveCreationMessageBlocks()
    {
        if (string.IsNullOrEmpty((await _queueStateManager.GetReviewInCreation())?.MessageTimestamp))
        {
            return [];
        }

        return
        [
            new DividerBlock(),
            new SectionBlock("If the creation message is stuck, it can be removed.")
            {
                BlockId = "creation_message",
                Accessory = new Button("Remove the creation message.")
                {
                    ActionId = "remove",
                    Confirm = new("Remove the creation message.", "You are about to remove the creation message. Please confirm.", "Remove", "Cancel")
                }
            },
        ];
    }

    private async Task<IList<IBlock>> CreatePullRequestAllowanceBlocks()
    {
        string allowancePrefix;
        if (await _queueStateManager.IsCreationAllowed())
            allowancePrefix = "Disallow";
        else
            allowancePrefix = "Allow";

        return
        [
            new DividerBlock(),
            new SectionBlock("Manage Pull Request Creation Allowance")
            {
                BlockId = "pull_requests_creation",
                Accessory = new Button($"{allowancePrefix} Creation")
                {
                    ActionId = allowancePrefix.ToLower(),
                    Confirm = new($"{allowancePrefix}ing to create pull requests",
                                    $"You are about to {allowancePrefix.ToLower()} the creation of pull requests. Please confirm.",
                                    allowancePrefix,
                                    "Cancel")
                }
            },
        ];
    }

    private static IList<IBlock> CreateMainSettingsBlocks(Setting setting)
    {
        return
        [
            new SectionBlock("Specify the channel to post messages")
            {
                BlockId = "channel_to_post",
                Accessory = new SelectPublicChannel()
                {
                    Placeholder = new("Specify the channel"),
                    ActionId = "select",
                    InitialChannel = setting.CreatePullRequestChannelId
                }
            },
            new SectionBlock("Select users who can administer this application, in addition to workspace admin(s).")
            {
                BlockId = "app_admins",
                Accessory = new MultiSelectConversations()
                {
                    Placeholder = new("Select application administrators"),
                    ActionId = "select",
                    InitialConversations = setting.ApplicationAdminUsers,
                    Filter = new() { Include = [Filter.ConversationType.DirectMessages], ExcludeBotUsers = true }
                }
            },
        ];
    }

    private async Task<IRequestResult> CreatePullRequest(BlockActionsPayload payload)
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
        var result = await _createPullRequestInvocation.CommandBindings[command.CommandText].Invoke(command);

        if (!result.IsSuccessful)
            await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id, ("pull_request", result.Error!)));

        return result;
    }

    private async Task<IRequestResult> SetChannelToPost(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        setting.CreatePullRequestChannelId = ((SelectPublicChannelState)payload.View.State.Values["channel_to_post"]["select"]).SelectedChannel;
        await _settingStore.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SetApplicationAdmins(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        setting.ApplicationAdminUsers = ((MultiSelectConversationsState)payload.View.State.Values["app_admins"]["select"]).SelectedConversations;
        await _settingStore.Save(setting);

        if (payload.View.Blocks.Any(b => b.BlockId.StartsWith("validation")))
            await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> RemoveCreationMessage(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;
        var reviewInCreation = await _queueStateManager.GetReviewInCreation();

        if (reviewInCreation != null)
        {
            await _queueStateManager.CancelCreation(payload.User.Id, true);
            await _slackClient.ChatDeleteMessage(setting.CreatePullRequestChannelId!, reviewInCreation.MessageTimestamp!);
            await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));
        }

        return RequestResult.Success();
    }

    private async Task<IRequestResult> ManageBranches(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of branches, separated by spaces. The first one will be selected by default for creation reviews.",
                           new PlainTextInput() { InitialValue = string.Join(" ", setting.Branches)})
            {
                BlockId = "branches"
            }
        ];

        var editBranchesModal = new ModalView("Manage Branches", blocks) { Submit = new("Save"), CallbackId = "edit_branches" };
        await _slackClient.ViewOpen(payload.TriggerId, editBranchesModal);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitManageBranches(ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["branches"].FirstOrDefault().Value is PlainTextInputState plainTextInputState)
        {
            var branches = plainTextInputState.Value.Split(' ').Distinct();
            var setting = (await _settingStore.Find())!;

            if (branches.Count() != setting.Branches.Count() || branches.Intersect(setting.Branches).Count() != setting.Branches.Count())
            {
                setting.Branches = branches;
                await _settingStore.Save(setting);
                await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));
            }
        }

        return RequestResult.Success();
    }

    private async Task<IRequestResult> ManageTags(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        IEnumerable<IBlock> blocks =
        [
            new InputBlock("List of tags, separated by spaces.", new PlainTextInput() { InitialValue = string.Join(" ", setting.Tags)})
            {
                BlockId = "tags"
            }
        ];

        var editBranchesModal = new ModalView("Manage Tags", blocks) { Submit = new("Save"), CallbackId = "edit_tags" };
        await _slackClient.ViewOpen(payload.TriggerId, editBranchesModal);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitEditTags(ViewSubmissionPayload payload)
    {
        if (payload.View.State.Values["tags"].FirstOrDefault().Value is PlainTextInputState plainTextInputState)
        {
            var tags = plainTextInputState.Value.Split(' ').Distinct();
            var setting = (await _settingStore.Find())!;

            if (tags.Count() != setting.Tags.Count() || tags.Intersect(setting.Tags).Count() != setting.Tags.Count())
            {
                setting.Tags = tags;
                await _settingStore.Save(setting);
                await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));
            }
        }

        return RequestResult.Success();
    }

    private async Task<IRequestResult> UpdateCreationAllowance(BlockActionsPayload payload)
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

        var setting = (await _settingStore.Find())!;
        await _slackClient.ChatPostMessage(new(setting.CreatePullRequestChannelId, channelMessage));
        await _slackClient.ConversationsSetTopic(setting.CreatePullRequestChannelId, topicMessage);
        await _slackClient.ViewPublish(payload.User.Id, await BuildHomeView(payload.User.Id));

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SetupReminder(BlockActionsPayload payload)
    {
        await _slackClient.ViewOpen(payload.TriggerId, await BuildConfigureReminderModal());
        return RequestResult.Success();
    }

    private async Task<ModalView> BuildConfigureReminderModal()
    {
        var setting = (await _settingStore.Find())!;
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
                new InputBlock("Specify the channel to use for reminders.", new SelectPublicChannel() { InitialChannel = reminderSetting.RemindingChannelId })
                {
                    BlockId = "channel"
                },
                new InputBlock("Specify the reminder message template", new PlainTextInput() { InitialValue = reminderSetting.MessageTemplate })
                {
                    BlockId = "template"
                },
                new InputBlock("Enter the period (in minutes) after which to send the reminder.", new NumberInput()
                {
                    InitialValue = reminderSetting.TimeToRemindInMinutes.ToString(),
                    MinValue = "1"
                })
                {
                    BlockId = "minutes"
                },
                new InputBlock("Workday starts at (UTC):", new TimePicker() { InitialTime = reminderSetting.WorkDayStart })
                {
                    BlockId = "day_starts"
                },
                new InputBlock("Workday ends at (UTC):", new TimePicker() { InitialTime = reminderSetting.WorkDayEnd, })
                {
                    BlockId = "day_ends"
                }
            ]);

        return new ModalView("Configure reminder", blocks) { Submit = new("Save"), CallbackId = "configure_reminder" };
    }

    private async Task<IRequestResult> EnableReminder(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        setting.ReminderSetting ??= new ReminderSetting();
        setting.ReminderSetting.Enabled = !setting.ReminderSetting.Enabled;

        await _settingStore.Save(setting);
        await _slackClient.ViewUpdate(payload.View.RootViewId, await BuildConfigureReminderModal());
        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitConfigureReminder(ViewSubmissionPayload payload)
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

    private async Task<IRequestResult> SetupWebhook(BlockActionsPayload payload)
    {
        await _slackClient.ViewOpen(payload.TriggerId, await BuildConfigureWebhookModal());
        return RequestResult.Success();
    }

    private async Task<ModalView> BuildConfigureWebhookModal(Dictionary<string, string>? selectedVersionStrategies = null)
    {
        var setting = (await _settingStore.Find())!;
        WebhookSetting webhookSetting = setting.WebhookSetting ?? new();

        List<IBlock> blocks =
        [
            new InputBlock("Enter the webhook URL", new UrlInput()
            {
                InitialValue = webhookSetting.WebhookUrl
            })
            {
                BlockId = "url"
            },
            new InputBlock("Enter the webhook header name to use for passing the secret.", new PlainTextInput()
            {
                InitialValue = webhookSetting.WebhookHeader
            })
            {
                BlockId = "header"
            },
            new InputBlock($"Enter the webhook secret. ({(string.IsNullOrEmpty(webhookSetting.WebhookSecret) ? "not " : "")}set)", new PlainTextInput())
            {
                BlockId = "secret",
                Optional = true
            },
            new InputBlock("Enter message template", new PlainTextInput()
            {
                InitialValue = webhookSetting.MessageTemplate,
                Multiline = true
            })
            {
                BlockId = "template"
            },
            new ContextBlock([new PlainText(@"Variables:
%Issues% - Issues in the current pull request
%Versions% - Build versions

By default, values are in a comma-separated string format. To convert them to an array, wrap them in square brackets (e.g., [%Issues%]).")])
        ];

        blocks.Add(new DividerBlock());
        foreach (string branch in setting.Branches)
        {
            webhookSetting.VersionStrategies.TryGetValue(branch, out var storedStrategy);

            IVersionStrategist strategist;
            if (selectedVersionStrategies?.TryGetValue(branch, out var strategyName) ?? false)
            {
                strategist = _versionStrategistResolver.GetStrategist(strategyName);
            }
            else
            {
                strategist = _versionStrategistResolver.GetStrategist(storedStrategy?.Name ?? "none");
            }

            blocks.AddRange([
                new SectionBlock(new MarkdownText($"Select the version strategy for `{branch}`:"))
                {
                    BlockId = branch,
                    Accessory = new RadioButton(
                        _versionStrategistResolver.GetAllStrategists().
                            Select(x => new Option<PlainText>(new(x.Description), x.Name))
                    )
                    {
                        ActionId = "select_version_strategy",
                        InitialOption = new Option<PlainText>(new(strategist.Description), strategist.Name)
                    }
                }
            ]);

            var strategyBlocks = strategist.GetBlocks(storedStrategy?.Name == strategist.Name ? storedStrategy.Values : [], x => $"{branch}_{x}");
            blocks.AddRange(strategyBlocks);
        }

        return new ModalView("Configure webhook", blocks) { Submit = new("Save"), CallbackId = "configure_webhook" };
    }

    private async Task<IRequestResult> SubmitConfigureWebhook(ViewSubmissionPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        setting.WebhookSetting ??= new WebhookSetting();

        setting.WebhookSetting.WebhookUrl = ((UrlInputState)payload.View.State.Values["url"].First().Value).Value;
        setting.WebhookSetting.WebhookHeader = ((PlainTextInputState)payload.View.State.Values["header"].First().Value).Value;

        var webhookSecret = ((PlainTextInputState)payload.View.State.Values["secret"].First().Value).Value;
        if (!string.IsNullOrEmpty(webhookSecret)) setting.WebhookSetting.WebhookSecret = webhookSecret;
        setting.WebhookSetting.MessageTemplate = ((PlainTextInputState)payload.View.State.Values["template"].First().Value).Value;

        setting.WebhookSetting.VersionStrategies = setting.Branches
            .Select(b =>
            {
                var strategyName = ((RadioButtonState)payload.View.State.Values[b].First().Value).SelectedOption!.Value;
                var strategist = _versionStrategistResolver.GetStrategist(strategyName);
                var strategyValues = payload.View.State.Values
                    .Where(s => s.Key.StartsWith($"{b}_"))
                    .ToDictionary(s => s.Key.Replace($"{b}_", ""), s => s.Value.First().Value);

                return new
                {
                    Key = b,
                    Value = new VersionStrategy
                    {
                        Name = strategist.Name,
                        Values = strategist.ToDictionary(strategyValues)
                    }
                };
            })
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value!);

        await _settingStore.Save(setting);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SelectVersionStrategy(BlockActionsPayload payload)
    {
        var setting = (await _settingStore.Find())!;

        var selectedStrategies = setting.Branches
                                    .Select(x => new { Key = x, ((RadioButtonState)payload.View.State.Values[x].First().Value).SelectedOption!.Value })
                                    .ToDictionary(x => x.Key, x => x.Value);

        await _slackClient.ViewUpdate(payload.View.RootViewId, await BuildConfigureWebhookModal(selectedStrategies));
        return RequestResult.Success();
    }
}
