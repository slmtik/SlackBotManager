using SlackBotManager.API.Invocations;
using SlackBotManager.Persistence;
using SlackBotManager.Slack;
using SlackBotManager.Slack.Blocks;
using SlackBotManager.Slack.Commands;
using SlackBotManager.Slack.Elements;
using SlackBotManager.Slack.ElementStates;
using SlackBotManager.Slack.Payloads;
using SlackBotManager.Slack.Views;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlackBotManager.API.Services;

public class PullRequestInvocation : ICommandInvocation, IViewSubmissionInvocation, IViewClosedInvocation, IBlockActionsInvocation
{
    private class ViewMetadata(string channelId, string timestamp)
    {
        public string ChannelId { get; set; } = channelId;
        public string Timestamp { get; set; } = timestamp;
        public IEnumerable<string> Branches { get; set; } = [];
        public int IssuesNumber { get; set; }
    }

    private class MessageMetadata(string pullRequestAuthor, IEnumerable<string> branches)
    {
        public string PullRequestAuthor { get; set; } = pullRequestAuthor;
        public IEnumerable<string> Branches { get; set; } = branches;
        public List<string>? Reviewing { get; set; }
        public List<string>? Approved { get; set; }
        public Dictionary<string, Profile>? UserProfiles { get; set; }

        public static explicit operator JsonObject(MessageMetadata messageMetadata)
        {
            var jsonValues = new List<KeyValuePair<string, JsonNode?>>()
            {
                new ("pull_request_author", messageMetadata.PullRequestAuthor),
                new ("branches", JsonValue.Create(messageMetadata.Branches)),
                new ("reviewing", JsonValue.Create(messageMetadata.Reviewing)),
                new ("approved", JsonValue.Create(messageMetadata.Approved)),
                new ("user_profiles", JsonValue.Create(messageMetadata.UserProfiles))
            };

            return new JsonObject(jsonValues);
        }

        public static explicit operator MessageMetadata(JsonObject json)
        {
            return new MessageMetadata(json["pull_request_author"]?.ToString() ?? throw new ArgumentNullException(nameof(json)),
                                       json["branches"]?.Deserialize<IEnumerable<string>>() ?? throw new ArgumentNullException(nameof(json)))
            {
                Reviewing = json["reviewing"].Deserialize<List<string>>(),
                Approved = json["approved"].Deserialize<List<string>>(),
                UserProfiles = json["user_profiles"].Deserialize<Dictionary<string, Profile>>(SlackClient.SlackJsonSerializerOptions)
            };
        }
    }

    private readonly ISettingStore _settingStore;
    private readonly QueueStateManager _queueStateManager;

    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> ViewSubmissionBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> ViewClosedBindings { get; } = [];
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> BlockActionsBindings { get; } = [];

    public PullRequestInvocation(ISettingStore settingStore, QueueStateManager queueStateManager)
    {
        _settingStore = settingStore;
        _queueStateManager = queueStateManager;

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

    private async Task<IRequestResult> ShowCreatePullRequestView(SlackClient client, Command commandRequest)
    {
        var setting = await _settingStore.Find();

        if (string.IsNullOrEmpty(setting?.CreatePullRequestChannelId))
            return RequestResult.Failure("Please set the *Channel to post Messages* setting");

        var availableBranches = await _queueStateManager.GetAvailableBranches(commandRequest.UserId);
        if (!availableBranches.Any())
            return RequestResult.Failure("There is no available branch to create pull request in");

        var messageTimestamp = (await _queueStateManager.StartCreation(commandRequest.UserId)).MessageTimestamp;

        if (string.IsNullOrEmpty(messageTimestamp))
        {
            var userInfoResult = await client.UserInfo(commandRequest.UserId);

            if (!userInfoResult.IsSuccesful)
                return RequestResult.Failure("Failed to get user info. Please check logs");

            string message = $"{userInfoResult.Value!.User.Profile.DisplayName} is creating a new pull request";
            var postMessageResult = await client.ChatPostMessage(new(setting?.CreatePullRequestChannelId!, message));
            if (!postMessageResult.IsSuccesful)
                return postMessageResult.Error switch
                {
                    "not_in_channel" => RequestResult.Failure("Please add the App to the channel from the *Channel to post Messages* setting"),
                    _ => RequestResult.Failure("Unknown error")
                };

            messageTimestamp = postMessageResult.Value!.Timestamp;
            await _queueStateManager.UpdateCreation(commandRequest.UserId, messageTimestamp);
        }

        var viewMetadata = new ViewMetadata(setting?.CreatePullRequestChannelId!, messageTimestamp)
        {
            Branches = availableBranches.First() == setting!.Branches.First() ? [setting!.Branches.First()] : [],
            IssuesNumber = 1
        };
        await client.ViewOpen(commandRequest.TriggerId, BuildCreatePullRequestModal(viewMetadata, setting.Tags));

        return RequestResult.Success();
    }

    private static ModalView BuildCreatePullRequestModal(ViewMetadata viewMetadata, IEnumerable<string> tags)
    {
        List<IBlock> blockList = [];

        for (int i = 0; i < Math.Max(1, viewMetadata.Branches.Count()); i++)
            blockList.Add(new InputBlock("Pull Request Link", new UrlInput() { ActionId = "url_input" }) { BlockId = $"pull_request_{i}" });

        for (int i = 0; i < viewMetadata.IssuesNumber; i++)
            blockList.Add(new InputBlock("Issue Link", new UrlInput() { ActionId = "url_input" }) { BlockId = $"issue_tracker_{i}" });

        blockList.Add(new InputBlock("Tags", new MultiStaticSelect(tags.Select(t => new Option<PlainText>(new(t), t))) { ActionId = "select" })
        {
            BlockId = "tags",
            Optional = true
        });
        blockList.Add(new SectionBlock("Click to set branches or change number of issues")
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

    private async Task<IRequestResult> SubmitCreatePullRequestView(SlackClient client, ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (!viewMetadata.Branches.Any())
        {
            var submitValidationError = new ErrorSubmissionResponse();
            submitValidationError.Errors.Add("pull_request_0", "You have to select branch in the Manage details");
            return RequestResult.Failure(JsonSerializer.Serialize<ISubmissionResponse>(submitValidationError));
        }

        await client.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);

        var pullRequestLinks = Enumerable.Range(0, viewMetadata.Branches.Count())
            .Select(i => payload.View.State.Values[$"pull_request_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis => $"<{uis.Value}|Pull Request>")
            .ToList();

        var issueLinks = Enumerable.Range(0, viewMetadata.IssuesNumber)
            .Select(i => payload.View.State.Values[$"issue_tracker_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis =>
            {
                Uri.TryCreate(uis.Value, UriKind.Absolute, out Uri? issueUri);
                return $"<{uis.Value}|{issueUri?.Segments.Last()}>";
            })
            .ToList();

        IEnumerable<string> tags = [];

        if (payload.View.State.Values["tags"]["select"] is MultiStaticSelectState selectedTags
            && selectedTags.SelectedOptions is not null)
        {
            tags = selectedTags.SelectedOptions.Select(o => o.Value);
        }

        var linkSectionBlock = Enumerable.Range(0, Math.Max(pullRequestLinks.Count, issueLinks.Count))
            .SelectMany(index => new[] { pullRequestLinks.ElementAtOrDefault(index) ?? " ", issueLinks.ElementAtOrDefault(index) ?? " " })
            .Select(link => new MarkdownText(link))
            .Chunk(10)
            .Select(chunk => new SectionBlock(chunk));

        List<IBlock> blocks =
        [
            new SectionBlock(
                new MarkdownText($"<@{payload.User.Id}> *has created a new pull request* `{string.Join(" ", viewMetadata.Branches)}`"))
            {
                BlockId = "sectionBlockOnlyMrkdwn"
            },
            new DividerBlock()
        ];

        blocks.AddRange(linkSectionBlock);

        blocks.Add(
            new ActionBlock(
            [
                new Button("Review :eyes:") { ActionId = "review" },
                new Button("Approve :white_check_mark:") { ActionId = "approve" },
                new Button("Merge :checkered_flag:")
                {
                    ActionId = "merge",
                    Style = "primary",
                    Confirm = new("Merge the pull request", "You are going to Merge the pull request. Please confirm", "Merge", "Cancel")
                },
                new Button("Close :x:")
                {
                    ActionId = "close",
                    Style = "danger",
                    Confirm = new("Close the pull request", "You are going to Close the pull request. Please confirm", "Close", "Cancel")
                    {
                        Style = "danger"
                    }
                },
            ]) { BlockId = "pull_request_status" });

        if (tags.Any())
        {
            blocks.Add(new ContextBlock(tags.Select(t => new PlainText(t))));
        }

        var postMessageResult = await client.ChatPostMessage(new ChatPostMessageRequest(viewMetadata.ChannelId, blocks)
        {
            UnfurlLinks = true,
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = (JsonObject)new MessageMetadata(payload.User.Id, viewMetadata.Branches)
            }
        });

        if (postMessageResult.IsSuccesful)
            await _queueStateManager.FinishCreation(payload.User.Id, postMessageResult.Value!.Timestamp, viewMetadata.Branches);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> SubmitManageDetailsView(SlackClient client, ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (payload.View.State.Values["branches"]["multi_static_select"] is MultiStaticSelectState multiStaticSelectState)
            viewMetadata.Branches = (multiStaticSelectState.SelectedOptions ?? []).Select(so => so.Value).ToList();

        if (payload.View.State.Values["issues"]["number_input"] is NumberInputState numberInputState)
            viewMetadata.IssuesNumber = int.Parse(numberInputState.Value);

        var setting = await _settingStore.Find();
        var viewToCreatePullRequest = BuildCreatePullRequestModal(viewMetadata, setting.Tags);
        var viewId = payload.View.RootViewId;

        await client.ViewUpdate(viewId, viewToCreatePullRequest);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> CloseCreatePullRequestView(SlackClient client, ViewClosedPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;
        await client.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);

        await _queueStateManager.CancelCreation(payload.User.Id);

        return RequestResult.Success();
    }

    private async Task<IRequestResult> ShowManageDetailsView(SlackClient client, BlockActionsPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        await client.ViewPush(payload.TriggerId, BuildManageDetailsModal(viewMetadata, await _queueStateManager.GetAvailableBranches(payload.User.Id)));

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

    private async Task<IRequestResult> UpdatePullRequestStatus(SlackClient client, BlockActionsPayload payload)
    {
        var pullRequestStatus = payload.Actions.First().ActionId;
        if (!await UpdateReviewers(client, payload.Message!, pullRequestStatus, payload.User.Id))
            return RequestResult.Success();

        await client.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = payload.Message.Metadata });

        string threadMessage;
        if (pullRequestStatus.Equals("review"))
            threadMessage = $"<@{payload.User.Id}> started reviewing";
        else if (pullRequestStatus.Equals("approve"))
        {
            var displayName = ((MessageMetadata)payload.Message.Metadata.EventPayload!).UserProfiles![payload.User.Id].DisplayName;
            threadMessage = $"{displayName} approved the pull request";
        }
        else
            return RequestResult.Success();

        await client.ChatPostMessage(new(payload.Channel.Id, threadMessage) { ThreadTimestamp = payload.Message.Timestamp });

        return RequestResult.Success();
    }

    private async static Task<bool> UpdateReviewers(SlackClient client, Message message, string pullRequestStatus, string userId)
    {
        var messageMetadata = (MessageMetadata)message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];
        messageMetadata.Approved ??= [];

        if (pullRequestStatus.Equals("review") && !messageMetadata.Reviewing.Contains(userId))
            messageMetadata.Reviewing.Add(userId);
        else if (pullRequestStatus.Equals("approve") && !messageMetadata.Approved.Contains(userId) && messageMetadata.Reviewing.Contains(userId))
            messageMetadata.Approved.Add(userId);
        else
            return false;

        var messageBlocks = message.Blocks.ToList();
        messageBlocks.Remove(messageBlocks.Find(b => b is ContextBlock && b.BlockId == "reviewers")!);

        messageMetadata.UserProfiles ??= [];

        if (!messageMetadata.UserProfiles.ContainsKey(userId))
            messageMetadata.UserProfiles[userId] = (await client.UserInfo(userId)).Value.User.Profile;

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

    private async Task<IRequestResult> FinishPullRequest(SlackClient client, BlockActionsPayload payload)
    {
        var messageMetadata = (MessageMetadata)payload.Message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];

        var pullRequestStatus = payload.Actions.First().ActionId;
        var currentUserId = payload.User.Id;

        if (!messageMetadata.Reviewing.Contains(currentUserId) && !(pullRequestStatus.Equals("close") && messageMetadata.PullRequestAuthor.Equals(currentUserId)))
            return RequestResult.Success();

        var (message, messageBlocks) = (payload.Message, payload.Message.Blocks.ToList());
        messageBlocks.RemoveAll(b => b is ContextBlock && b.BlockId == "reviewers" || b is ActionBlock && b.BlockId == "pull_request_status");

        messageMetadata.UserProfiles ??= [];

        string displayName;
        if (messageMetadata.UserProfiles.TryGetValue(payload.User.Id, out Profile? profile))
            displayName = profile.DisplayName;
        else
            displayName = (await client.UserInfo(payload.User.Id)).Value.User.Profile.DisplayName;

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
            return RequestResult.Success();

        messageBlocks.Where(b => b is SectionBlock && b.BlockId == "sectionBlockOnlyMrkdwn").Cast<SectionBlock>().Single().Text.Text = channelMessage;
        payload.Message.Blocks = messageBlocks;

        await client.ChatPostMessage(new(payload.Channel.Id, threadMessage) { ThreadTimestamp = payload.Message.Timestamp });
        await client.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = null });

        await _queueStateManager.FinishReview(payload.Message.Timestamp, messageMetadata.Branches);

        return RequestResult.Success();
    }
}
