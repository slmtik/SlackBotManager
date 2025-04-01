using API.Interfaces.Invocations;
using Core.ApiClient;
using Persistence.Interfaces;
using Slack;
using Slack.DTO;
using Slack.Interfaces;
using Slack.Models.Blocks;
using Slack.Models.Commands;
using Slack.Models.Elements;
using Slack.Models.ElementStates;
using Slack.Models.Payloads;
using Slack.Models.Views;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace API.Services;

public class PullRequestInvocation : ICommandInvocation, IViewSubmissionInvocation, IViewClosedInvocation, IBlockActionsInvocation
{
    private class ViewMetadata(string channelId, string timestamp)
    {
        public string ChannelId { get; set; } = channelId;
        public string Timestamp { get; set; } = timestamp;
        public IEnumerable<string> Branches { get; set; } = [];
        public int IssuesNumber { get; set; }
    }

    private class MessageMetadata(string pullRequestAuthor, IEnumerable<string> branches, IEnumerable<string> issues, bool updateVersion)
    {
        public string PullRequestAuthor { get; set; } = pullRequestAuthor;
        public IEnumerable<string> Branches { get; set; } = branches;
        public IEnumerable<string> Issues { get; set; } = issues;
        public bool UpdateVersion { get; } = updateVersion;
        public List<string>? Reviewing { get; set; }
        public List<string>? Approved { get; set; }
        public Dictionary<string, Profile>? UserProfiles { get; set; }

        public static explicit operator JsonObject(MessageMetadata messageMetadata) => new()
        {
            ["pull_request_author"] = messageMetadata.PullRequestAuthor,
            ["branches"] = JsonValue.Create(messageMetadata.Branches),
            ["issues"] = JsonValue.Create(messageMetadata.Issues),
            ["reviewing"] = JsonValue.Create(messageMetadata.Reviewing),
            ["approved"] = JsonValue.Create(messageMetadata.Approved),
            ["user_profiles"] = JsonValue.Create(messageMetadata.UserProfiles),
            ["update_version"] = JsonValue.Create(messageMetadata.UpdateVersion)
        };

        public static explicit operator MessageMetadata(JsonObject json) => new(
            json["pull_request_author"]?.ToString() ?? throw new ArgumentNullException(nameof(json)),
            json["branches"]?.Deserialize<IEnumerable<string>>() ?? throw new ArgumentNullException(nameof(json)),
            json["issues"]?.Deserialize<IEnumerable<string>>() ?? throw new ArgumentNullException(nameof(json)),
            json["update_version"]?.GetValue<bool>() ?? throw new ArgumentNullException(nameof(json)))
        {
            Reviewing = json["reviewing"]?.Deserialize<List<string>>(),
            Approved = json["approved"]?.Deserialize<List<string>>(),
            UserProfiles = json["user_profiles"]?.Deserialize<Dictionary<string, Profile>>(SlackClient.ApiJsonSerializerOptions)
        };
    }

    private readonly SlackClient _slackClient;
    private readonly ISettingStore _settingStore;
    private readonly QueueStateManager _queueStateManager;
    private readonly WebhookSender _webhookSender;

    public Dictionary<string, Func<Command, Task<IRequestResult>>> CommandBindings { get; } = [];
    public Dictionary<string, Func<ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; } = [];
    public Dictionary<string, Func<ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; } = [];
    public Dictionary<(string? BlockId, string? ActionId), Func<BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; } = [];

    public PullRequestInvocation(SlackClient slackClient,ISettingStore settingStore, QueueStateManager queueStateManager, WebhookSender webhookSender)
    {
        _slackClient = slackClient;
        _settingStore = settingStore;
        _queueStateManager = queueStateManager;
        _webhookSender = webhookSender;
        CommandBindings.Add("/create_pull_request", ShowCreatePullRequestView);

        ViewSubmissionBindings.Add("create_pull_request", SubmitCreatePullRequestView);
        ViewSubmissionBindings.Add("manage_details", SubmitManageDetailsView);

        ViewClosedBindings.Add("create_pull_request", CloseCreatePullRequestView);

        BlockActionsBindings.Add(("details", "change"), ShowManageDetailsView);
        BlockActionsBindings.Add(("pull_request_status", "review"), UpdatePullRequestStatus);
        BlockActionsBindings.Add(("pull_request_status", "approve"), UpdatePullRequestStatus);
        BlockActionsBindings.Add(("pull_request_status", "merge"), FinishPullRequest);
        BlockActionsBindings.Add(("pull_request_status", "close"), FinishPullRequest);
    }

    private async Task<IRequestResult> ShowCreatePullRequestView(Command commandRequest)
    {
        var setting = await _settingStore.Find();

        if (string.IsNullOrEmpty(setting?.CreatePullRequestChannelId))
        {
            return RequestResult.Failure("Please specify the channel to post messages the settings");
        }

        var availableBranches = await _queueStateManager.GetAvailableBranches(commandRequest.UserId);
        if (!availableBranches.Any())
        {
            return RequestResult.Failure("No available branches to create a pull request.");
        }

        var messageTimestamp = (await _queueStateManager.StartCreation(commandRequest.UserId)).MessageTimestamp;

        if (string.IsNullOrEmpty(messageTimestamp))
        {
            var userInfoResult = await _slackClient.UserInfo(commandRequest.UserId);

            if (!userInfoResult.IsSuccessful)
            {
                return RequestResult.Failure("Failed to get user info. Please check logs");
            }

            string message = $"{userInfoResult.Value!.User.Profile.DisplayName} is creating a new pull request";
            var postMessageResult = await _slackClient.ChatPostMessage(new(setting?.CreatePullRequestChannelId!, message));
            if (!postMessageResult.IsSuccessful)
            {
                return postMessageResult.Error switch
                {
                    "not_in_channel" => RequestResult.Failure("Please add the App to the channel from the *Channel to post Messages* setting"),
                    _ => RequestResult.Failure("Unknown error")
                };
            }

            messageTimestamp = postMessageResult.Value!.Timestamp;
            await _queueStateManager.UpdateCreation(commandRequest.UserId, messageTimestamp);
        }

        var viewMetadata = new ViewMetadata(setting?.CreatePullRequestChannelId!, messageTimestamp)
        {
            Branches = availableBranches.First() == setting!.Branches.First() ? [setting!.Branches.First()] : [],
            IssuesNumber = 1
        };
        await _slackClient.ViewOpen(commandRequest.TriggerId, BuildCreatePullRequestModal(viewMetadata, setting.Tags));

        return RequestResult.Success();
    }

    private static ModalView BuildCreatePullRequestModal(ViewMetadata viewMetadata, IEnumerable<string> tags)
    {
        List<IBlock> blockList = [];

        for (int i = 0; i < Math.Max(1, viewMetadata.Branches.Count()); i++)
        {
            blockList.Add(new InputBlock("Pull Request Link", new UrlInput() { ActionId = "url_input" }) 
            { 
                BlockId = $"pull_request_{i}" 
            });
        }

        for (int i = 0; i < viewMetadata.IssuesNumber; i++)
        {
            blockList.Add(new InputBlock("Issue Link", new UrlInput() { ActionId = "url_input" }) 
            { 
                BlockId = $"issue_tracker_{i}" 
            });
        }

        blockList.Add(new InputBlock("Tags", new MultiStaticSelect(tags.Select(t => new Option<PlainText>(new(t), t))) 
        { 
            ActionId = "select" 
        })
        {
            BlockId = "tags",
            Optional = true
        });

        blockList.Add(new InputBlock("Update the version for issues in the issue tracker?", new StaticSelect([new(new("Yes"), "yes"), new(new("No"), "no")])
        {
            ActionId = "select",
            InitialOption = new(new("Yes"), "yes")
        })
        { 
            BlockId = "update_version" 
        });

        blockList.Add(new SectionBlock("Click to set branches or modify the issue number.")
        {
            Accessory = new Button("Manage details") { ActionId = "change" },
            BlockId = "details",
        });

        return new ModalView("Create Pull Request", blockList)
        {
            NotifyOnClose = true,
            CallbackId = "create_pull_request",
            Submit = new("Submit"),
            PrivateMetadata = JsonSerializer.Serialize(viewMetadata)
        };
    }

    private async Task<IRequestResult> SubmitCreatePullRequestView(ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (!viewMetadata.Branches.Any())
        {
            var submitValidationError = new ErrorSubmissionResponse();
            submitValidationError.Errors.Add("pull_request_0", "You have to select branch in the Manage details");
            return RequestResult.Failure(JsonSerializer.Serialize<ISubmissionResponse>(submitValidationError));
        }

        await _slackClient.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);

        var pullRequestLinks = Enumerable.Range(0, viewMetadata.Branches.Count())
            .Select(i => payload.View.State.Values[$"pull_request_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis => $"<{uis.Value}|Pull Request>")
            .ToList();

        var issues = Enumerable.Range(0, viewMetadata.IssuesNumber)
            .Select(i => payload.View.State.Values[$"issue_tracker_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis =>
            {
                Uri.TryCreate(uis.Value, UriKind.Absolute, out Uri? issueUri);
                string issueKey = issueUri?.Segments.Last() ?? "Unknown issue";
                return new
                {
                    Link = $"<{uis.Value}|{issueKey}>",
                    Key = issueKey
                };
            })
            .ToList();

        IEnumerable<string> tags = [];
        if (payload.View.State.Values["tags"]["select"] is MultiStaticSelectState selectedTags && selectedTags.SelectedOptions is not null)
        {
            tags = selectedTags.SelectedOptions.Select(o => o.Value);
        }

        var updateVersion = payload.View.State.Values["update_version"]["select"] is StaticSelectState { SelectedOption.Value: "yes" };

        var linkSectionBlock = Enumerable.Range(0, Math.Max(pullRequestLinks.Count, issues.Count))
            .SelectMany(index => new[] { pullRequestLinks.ElementAtOrDefault(index) ?? " ", issues.ElementAtOrDefault(index)?.Link ?? " " })
            .Select(link => new MarkdownText(link))
            .Chunk(10)
            .Select(chunk => new SectionBlock(chunk));

        List<IBlock> blocks =
        [
            new SectionBlock(new MarkdownText($"<@{payload.User.Id}> *has created a new pull request* `{string.Join(" ", viewMetadata.Branches)}`"))
            {
                BlockId = "sectionBlockOnlyMrkdwn"
            },
            new DividerBlock(),
            .. linkSectionBlock,
            new ActionBlock(
            [
                new Button("Review :eyes:") { ActionId = "review" },
                new Button("Approve :white_check_mark:") { ActionId = "approve" },
                new Button("Merge :checkered_flag:")
                {
                    ActionId = "merge",
                    Style = "primary",
                    Confirm = new("Merge the pull request", "You are about to merge the pull request. Do you wish to proceed?", "Merge", "Cancel")
                },
                new Button("Close :x:")
                {
                    ActionId = "close",
                    Style = "danger",
                    Confirm = new("Close the pull request", "You are about to close the pull request. Do you wish to proceed?", "Close", "Cancel")
                    {
                        Style = "danger"
                    }
                },
            ])
            { 
                BlockId = "pull_request_status" 
            },
        ];

        if (tags.Any())
        {
            blocks.Add(new ContextBlock(tags.Select(t => new PlainText(t))));
        }

        var postMessageResult = await _slackClient.ChatPostMessage(new ChatPostMessageRequest(viewMetadata.ChannelId, blocks)
        {
            UnfurlLinks = true,
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = (JsonObject)new MessageMetadata(payload.User.Id, viewMetadata.Branches, issues.Select(i => i.Key), updateVersion)
            }
        });

        if (postMessageResult.IsSuccessful)
            await _queueStateManager.FinishCreation(payload.User.Id, postMessageResult.Value!.Timestamp, viewMetadata.Branches);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitManageDetailsView(ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (payload.View.State.Values["branches"]["multi_static_select"] is MultiStaticSelectState multiStaticSelectState)
            viewMetadata.Branches = [.. (multiStaticSelectState.SelectedOptions ?? []).Select(so => so.Value)];

        if (payload.View.State.Values["issues"]["number_input"] is NumberInputState numberInputState)
            viewMetadata.IssuesNumber = int.Parse(numberInputState.Value);

        var setting = await _settingStore.Find();
        var viewToCreatePullRequest = BuildCreatePullRequestModal(viewMetadata, setting.Tags);
        var viewId = payload.View.RootViewId;

        await _slackClient.ViewUpdate(viewId, viewToCreatePullRequest);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> CloseCreatePullRequestView(ViewClosedPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;
        await _slackClient.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);

        await _queueStateManager.CancelCreation(payload.User.Id);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> ShowManageDetailsView(BlockActionsPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        await _slackClient.ViewPush(payload.TriggerId, BuildManageDetailsModal(viewMetadata, await _queueStateManager.GetAvailableBranches(payload.User.Id)));

        return RequestResult.Success();
    }

    private static ModalView BuildManageDetailsModal(ViewMetadata viewMetadata, IEnumerable<string> branches)
    {
        var initialBranches = branches.Where(b => viewMetadata.Branches.Contains(b)).Select(o => new Option<PlainText>(new(o), o)).ToList();

        return new ModalView("Manage Details",
        [
            new InputBlock("Branches",
                new MultiStaticSelect(branches.Select(b => new Option<PlainText>(new(b), b)))
                {
                    InitialOptions = initialBranches.Count > 0 ? initialBranches : null,
                    ActionId = "multi_static_select"
                })
            { BlockId = "branches" },
            new InputBlock("Number of issues in pull request",
                new NumberInput()
                {
                    IsDecimalAllowed = false,
                    ActionId = "number_input",
                    InitialValue = viewMetadata.IssuesNumber.ToString(),
                    MinValue = $"{1}",
                    MaxValue = $"{15}"
                })
            { BlockId = "issues" }
        ])
        {
            CallbackId = "manage_details",
            Submit = new("Submit"),
            PrivateMetadata = JsonSerializer.Serialize(viewMetadata)
        };
    }

    private async Task<IRequestResult> UpdatePullRequestStatus(BlockActionsPayload payload)
    {
        var pullRequestStatus = payload.Actions.First().ActionId;
        if (!await UpdateReviewers(payload.Message!, pullRequestStatus, payload.User.Id))
            return RequestResult.Success();

        await _slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) 
        { 
            Metadata = payload.Message.Metadata 
        });

        string threadMessage;
        if (pullRequestStatus.Equals("review"))
            threadMessage = $"<@{payload.User.Id}> started reviewing";
        else if (pullRequestStatus.Equals("approve"))
        {
            var displayName = ((MessageMetadata)payload.Message.Metadata.EventPayload!).UserProfiles![payload.User.Id].DisplayName;
            threadMessage = $"{displayName} approved the pull request";
        }
        else
        {
            return RequestResult.Success();
        }

        await _slackClient.ChatPostMessage(new(payload.Channel.Id, threadMessage) 
        { 
            ThreadTimestamp = payload.Message.Timestamp 
        });

        return RequestResult.Success();
    }

    private async Task<bool> UpdateReviewers(Message message, string pullRequestStatus, string currentUserId)
    {
        var messageMetadata = (MessageMetadata)message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];
        messageMetadata.Approved ??= [];

        if (pullRequestStatus.Equals("review") && !messageMetadata.Reviewing.Contains(currentUserId))
            messageMetadata.Reviewing.Add(currentUserId);
        else if (pullRequestStatus.Equals("approve") && !messageMetadata.Approved.Contains(currentUserId)
            && messageMetadata.Reviewing.Contains(currentUserId))
            messageMetadata.Approved.Add(currentUserId);
#if DEBUG
        else if (pullRequestStatus.Equals("review") && messageMetadata.PullRequestAuthor.Equals(currentUserId))
        {
            return false;
        }
#endif
        else
        { 
            return false; 
        }

        var messageBlocks = message.Blocks.ToList();
        messageBlocks.Remove(messageBlocks.Find(b => b is ContextBlock && b.BlockId == "reviewers")!);

        messageMetadata.UserProfiles ??= [];

        if (!messageMetadata.UserProfiles.ContainsKey(currentUserId))
            messageMetadata.UserProfiles[currentUserId] = (await _slackClient.UserInfo(currentUserId)).Value.User.Profile;

        var reviewersBlocks = new List<IContextElement>() { new PlainText("Reviewing:") };
        foreach (var item in messageMetadata.Reviewing)
            reviewersBlocks.Add(new Image(messageMetadata.UserProfiles[item].DisplayName, messageMetadata.UserProfiles[item].Image_24));

        if (messageMetadata.Approved.Count > 0)
        {
            reviewersBlocks.Add(new PlainText("Approved:"));
            foreach (var item in messageMetadata.Approved)
                reviewersBlocks.Add(new Image(messageMetadata.UserProfiles[item].DisplayName, messageMetadata.UserProfiles[item].Image_24));
        }

        var reviewersBlock = new ContextBlock(reviewersBlocks) { BlockId = "reviewers" };

        messageBlocks.Add(reviewersBlock);
        message.Blocks = messageBlocks;

        message.Metadata.EventPayload = (JsonObject)messageMetadata;

        return true;
    }

    private async Task<IRequestResult> FinishPullRequest(BlockActionsPayload payload)
    {
        var messageMetadata = (MessageMetadata)payload.Message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];

        var pullRequestStatus = payload.Actions.First().ActionId;
        var currentUserId = payload.User.Id;

        if (!messageMetadata.Reviewing.Contains(currentUserId) && !(pullRequestStatus.Equals("close")
            && messageMetadata.PullRequestAuthor.Equals(currentUserId)))
        {
            return RequestResult.Success();
        }

        var (message, messageBlocks) = (payload.Message, payload.Message.Blocks.ToList());
        messageBlocks.RemoveAll(b => b is ContextBlock && b.BlockId == "reviewers" || b is ActionBlock && b.BlockId == "pull_request_status");

        messageMetadata.UserProfiles ??= [];

        string displayName;
        if (messageMetadata.UserProfiles.TryGetValue(payload.User.Id, out Profile? profile))
            displayName = profile.DisplayName;
        else
            displayName = (await _slackClient.UserInfo(payload.User.Id)).Value.User.Profile.DisplayName;

        string threadMessage, channelMessage;
        if (pullRequestStatus.Equals("close"))
        {
            threadMessage = $"{displayName} closed the pull request";
            channelMessage = $"<@{messageMetadata.PullRequestAuthor}>*'s pull request was closed* :x: `{string.Join(" ", messageMetadata.Branches)}`";
        }
        else if (pullRequestStatus.Equals("merge"))
        {
            threadMessage = $"{displayName} merged the pull request";
            channelMessage = $"<@{messageMetadata.PullRequestAuthor}>*'s pull request merged* :checkered_flag: `{string.Join(" ", messageMetadata.Branches)}`";
        }
        else
        {
            return RequestResult.Success();
        }

        messageBlocks.Where(b => b is SectionBlock && b.BlockId == "sectionBlockOnlyMrkdwn").Cast<SectionBlock>().Single().Text.Text = channelMessage;
        payload.Message.Blocks = messageBlocks;

        await _slackClient.ChatPostMessage(new(payload.Channel.Id, threadMessage) { ThreadTimestamp = payload.Message.Timestamp });
        await _slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = null });

        if (messageMetadata.UpdateVersion)
        {
            await _queueStateManager.FinishReview(payload.Message.Timestamp, messageMetadata.Branches);
            if (pullRequestStatus.Equals("merge"))
            {
                var versionUpdateResult = await _webhookSender.SendMessage(messageMetadata.Branches, messageMetadata.Issues);
                if(versionUpdateResult.IsSuccessful)
                {
                    var versions = versionUpdateResult.Value ?? [];
                    string versionUpdateMessage = versions.Count > 0
                        ? $"The issue(s) will be updated with the following versions: {string.Join(", ", versions)}"
                        : "No version was generated.";

                    await _slackClient.ChatPostMessage(new(payload.Channel.Id, versionUpdateMessage) { ThreadTimestamp = payload.Message.Timestamp });
                }
            }
        }
        return RequestResult.Success();
    }
}
