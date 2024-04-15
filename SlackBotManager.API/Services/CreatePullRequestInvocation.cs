using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Blocks;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Models.Core;
using SlackBotManager.API.Models.Elements;
using SlackBotManager.API.Models.ElementStates;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Models.SlackClient;
using SlackBotManager.API.Models.Surfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlackBotManager.API.Services;

public class CreatePullRequestInvocation : ICommandInvocation, IViewSubmissionInvocation, IViewClosedInvocation, IBlockActionsInvocation
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
            return new MessageMetadata(json["pull_request_author"].ToString(),
                                       json["branches"].Deserialize<IEnumerable<string>>())
            { 
                Reviewing = json["reviewing"].Deserialize<List<string>>(),
                Approved = json["approved"].Deserialize<List<string>>(),
                UserProfiles = json["user_profiles"].Deserialize<Dictionary<string, Profile>>(SlackClient.SlackJsonSerializerOptions)
            };
        }
    }

    private readonly ISettingRepository _settingRepository;

    private enum PullRequestStatus
    {
        Review,
        Approve,
        Merge,
        Close
    }

    public Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> CommandBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task>> ViewSubmissionBindings { get; } = [];
    public Dictionary<string, Func<SlackClient, ViewClosedPayload, Task>> ViewClosedBindings { get; } = [];
    public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> BlockActionsBindings { get; } = [];

    public CreatePullRequestInvocation(ISettingRepository settingRepository)
    {
        CommandBindings.Add("/create_pull_request", ShowCreatePullRequestView);

        ViewSubmissionBindings.Add("create_pull_request", SubmitCreatePullRequestView);
        ViewSubmissionBindings.Add("manage_details", SubmitManageDetailsView);

        ViewClosedBindings.Add("create_pull_request", CloseCreatePullRequestView);

        BlockActionsBindings.Add(("details", "change"), ShowManageDetailsView);
        BlockActionsBindings.Add(("pull_request_status", "review"), UpdatePullRequestStatus);
        BlockActionsBindings.Add(("pull_request_status", "approve"), UpdatePullRequestStatus);
        BlockActionsBindings.Add(("pull_request_status", "merge"), FinishPullRequest);
        BlockActionsBindings.Add(("pull_request_status", "close"), FinishPullRequest);
        _settingRepository = settingRepository;
    }

    private async Task<IRequestResult> ShowCreatePullRequestView(SlackClient slackClient, Command commandRequest)
    {
        var setting = await _settingRepository.Find(commandRequest.EnterpriseId, commandRequest.TeamId, commandRequest.UserId, commandRequest.IsEnterpriseInstall);

        if (string.IsNullOrEmpty(setting?.CreatePullRequestChannelId))
            return RequestResult.Failure("Please set the *Channel to post Messages* setting");

        if (!string.IsNullOrWhiteSpace(setting.CurrentPullRequestReview?.AuthorId) && !setting.CurrentPullRequestReview.AuthorId.Equals(commandRequest.UserId))
            return RequestResult.Failure("There is already a pull request in the creation progress");

        string? creationMessageTimestamp = setting.CurrentPullRequestReview?.MessageTimestamp;
        if (string.IsNullOrEmpty(creationMessageTimestamp))
        {
            var userInfoResult = await slackClient.UserInfo(commandRequest.UserId);

            if (!userInfoResult.IsSuccesful)
                return RequestResult.Failure("Failed to get user info. Please check logs");

            string message = $"{userInfoResult.Value!.User.Profile.DisplayName} is creating a new pull request";
            var postMessageResult = await slackClient.ChatPostMessage(new(setting?.CreatePullRequestChannelId!, message));
            if (!postMessageResult.IsSuccesful)
                return postMessageResult.Error switch
                {
                    "not_in_channel" => RequestResult.Failure("Please add the App to the channel from the *Channel to post Messages* setting"),
                    _ => RequestResult.Failure("Unknown error")
                };
            creationMessageTimestamp = postMessageResult.Value!.Timestamp;
        }

        setting!.CurrentPullRequestReview = new() { AuthorId = commandRequest.UserId, MessageTimestamp = creationMessageTimestamp };
        await _settingRepository.Save(setting);

        var viewMetadata = new ViewMetadata(setting?.CreatePullRequestChannelId!, creationMessageTimestamp) { Branches = [setting!.Branches.First()], IssuesNumber = 1 };
        await slackClient.ViewOpen(commandRequest.TriggerId, BuildCreatePullRequestModal(viewMetadata, setting.Tags));
        
        return RequestResult.Success();
    }

    private static ModalView BuildCreatePullRequestModal(ViewMetadata viewMetadata, IEnumerable<string> tags)
    {
        List<IBlock> blockList = [];

        for (int i = 0; i < viewMetadata.Branches.Count(); i++)
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

    private async Task SubmitCreatePullRequestView(SlackClient slackClient, ViewSubmissionPayload viewSubmissionPayload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(viewSubmissionPayload.View.PrivateMetadata)!;

        await slackClient.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);

        var pullRequestLinks = Enumerable.Range(0, viewMetadata.Branches.Count())
            .Select(i => viewSubmissionPayload.View.State.Values[$"pull_request_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis => $"<{uis.Value}|Pull Request>")
            .ToList();

        var issueLinks = Enumerable.Range(0, viewMetadata.IssuesNumber)
            .Select(i => viewSubmissionPayload.View.State.Values[$"issue_tracker_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis =>
            {
                Uri.TryCreate(uis.Value, UriKind.Absolute, out Uri? issueUri);
                return $"<{uis.Value}|{issueUri?.Segments.Last()}>";
            })
            .ToList();

        IEnumerable<string> tags = [];

        if (viewSubmissionPayload.View.State.Values["tags"]["select"] is MultiStaticSelectState selectedTags
            && selectedTags.SelectedOptions is not null)
        {
            tags = selectedTags.SelectedOptions.Select(o => o.Value);
        }

        List<string> links = [];
        for (var i = 0; i < Math.Max(pullRequestLinks.Count, issueLinks.Count); i++)
        {
            links.Add(pullRequestLinks.ElementAtOrDefault(i) ?? " ");
            links.Add(issueLinks.ElementAtOrDefault(i) ?? " ");
        }

        List<IBlock> blocks =
        [
            new SectionBlock(
                new MarkdownText($"<@{viewSubmissionPayload.User.Id}> *has created a new pull request* `{string.Join(" ", viewMetadata.Branches)}`"))
            {
                BlockId = "sectionBlockOnlyMrkdwn"
            },
            new DividerBlock(),
            new SectionBlock(links.Select(l => new MarkdownText(l))) { BlockId = "sectionBlockOnlyFields" },
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
            ]) { BlockId = "pull_request_status" },
        ];

        if (tags.Any())
        {
            blocks.Add(new ContextBlock(tags.Select(t => new PlainText(t))));
        }

        var postMessageResult = await slackClient.ChatPostMessage(new ChatPostMessageRequest(viewMetadata.ChannelId, blocks)
        {
            UnfurlLinks = true,
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = (JsonObject)new MessageMetadata(viewSubmissionPayload.User.Id, viewMetadata.Branches)
            }
        });

        var setting = await _settingRepository.Find(viewSubmissionPayload.Enterprise?.Id,
                                               viewSubmissionPayload.Team?.Id,
                                               viewSubmissionPayload.User.Id,
                                               viewSubmissionPayload.IsEnterpriseInstall);
        setting.CurrentPullRequestReview = null;
        await _settingRepository.Save(setting);
    }

    private async Task SubmitManageDetailsView(SlackClient slackClient, ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (payload.View.State.Values["branches"]["multi_static_select"] is MultiStaticSelectState multiStaticSelectState)
            viewMetadata.Branches = (multiStaticSelectState.SelectedOptions ?? []).Select(so => so.Value).ToList();

        if (payload.View.State.Values["issues"]["number_input"] is NumberInputState numberInputState)
            viewMetadata.IssuesNumber = int.Parse(numberInputState.Value);

        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);
        var viewToCreatePullRequest = BuildCreatePullRequestModal(viewMetadata, setting.Tags);
        var viewId = payload.View.RootViewId;

        await slackClient.ViewUpdate(viewId, viewToCreatePullRequest);
    }

    private async Task CloseCreatePullRequestView(SlackClient slackClient, ViewClosedPayload viewClosedPayload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(viewClosedPayload.View.PrivateMetadata)!;
        await slackClient.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);
        
        var setting = await _settingRepository.Find(viewClosedPayload.Enterprise?.Id,
                                               viewClosedPayload.Team?.Id,
                                               viewClosedPayload.User.Id,
                                               viewClosedPayload.IsEnterpriseInstall);
        setting.CurrentPullRequestReview = null;
        await _settingRepository.Save(setting);
    }

    private async Task ShowManageDetailsView(SlackClient slackClient, BlockActionsPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;
        var setting = await _settingRepository.Find(payload.Enterprise?.Id, payload.Team?.Id, payload.User.Id, payload.IsEnterpriseInstall);
        await slackClient.ViewPush(payload.TriggerId, BuildManageDetailsModal(viewMetadata, setting.Branches));
    }

    private static ModalView BuildManageDetailsModal(ViewMetadata viewMetadata, IEnumerable<string> branches)
    {
        return new ModalView("Manage Details",
        [
            new InputBlock("Branches", 
                new MultiStaticSelect(branches.Select(b => new Option<PlainText>(new(b), b)))
                {
                    InitialOptions = branches.Where(b => viewMetadata.Branches.Contains(b)).Select(o => new Option<PlainText>(new(o), o)).ToList(),
                    ActionId = "multi_static_select"
                }) 
            { BlockId = "branches" },
            new InputBlock("Number of issues in pull request", 
                new NumberInput()
                {
                    IsDecimalAllowed = false,
                    ActionId = "number_input",
                    InitialValue = viewMetadata.IssuesNumber.ToString(),
                    MinValue = 1.ToString(),
                    MaxValue = 5.ToString()
                }) 
            { BlockId = "issues" }
        ])
        {
            CallbackId = "manage_details",
            Submit = new("Submit"),
            PrivateMetadata = JsonSerializer.Serialize(viewMetadata)
        };
    }

    private async Task UpdatePullRequestStatus(SlackClient slackClient, BlockActionsPayload payload)
    {
        var pullRequestStatus = (PullRequestStatus)Enum.Parse(typeof(PullRequestStatus), payload.Actions.First().ActionId, true);
        if (!await UpdateReviewers(slackClient, payload.Message!, pullRequestStatus, payload.User.Id))
            return;

        await slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = payload.Message.Metadata });

        string threadMessage;
        if (pullRequestStatus == PullRequestStatus.Review)
            threadMessage = $"<@{payload.User.Id}> started reviewing";
        else if (pullRequestStatus == PullRequestStatus.Approve)
        {
            var displayName = ((MessageMetadata)payload.Message.Metadata.EventPayload!).UserProfiles![payload.User.Id].DisplayName;
            threadMessage = $"{displayName} approved the pull request";
        }
        else
            return;

        await slackClient.ChatPostMessage(new(payload.Channel.Id, threadMessage) { ThreadTimestamp = payload.Message.Timestamp });
    }

    private async static Task<bool> UpdateReviewers(SlackClient slackClient, Message message, PullRequestStatus pullRequestStatus, string userId)
    {
        var messageMetadata = (MessageMetadata)message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];
        messageMetadata.Approved ??= [];

        if (pullRequestStatus == PullRequestStatus.Review && !messageMetadata.Reviewing.Contains(userId))
            messageMetadata.Reviewing.Add(userId);
        else if (pullRequestStatus == PullRequestStatus.Approve && !messageMetadata.Approved.Contains(userId) && messageMetadata.Reviewing.Contains(userId))
            messageMetadata.Approved.Add(userId);
        else
            return false;

        var messageBlocks = message.Blocks.ToList();
        messageBlocks.Remove(messageBlocks.Find(b => b is ContextBlock && b.BlockId == "reviewers")!);

        messageMetadata.UserProfiles ??= [];

        if (!messageMetadata.UserProfiles.ContainsKey(userId))
            messageMetadata.UserProfiles[userId] = (await slackClient.UserInfo(userId)).Value.User.Profile;

        var reviewersBlocks = new List<IElement>() { new PlainText("Reviewing:") };
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

    private async Task FinishPullRequest(SlackClient slackClient, BlockActionsPayload payload)
    {
        var messageMetadata = (MessageMetadata)payload.Message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];
        
        var pullRequestStatus = (PullRequestStatus)Enum.Parse(typeof(PullRequestStatus), payload.Actions.First().ActionId, true);
        var currentUserId = payload.User.Id;

        if (!messageMetadata.Reviewing.Contains(currentUserId)
            && !(pullRequestStatus == PullRequestStatus.Close && messageMetadata.PullRequestAuthor.Equals(currentUserId)))
            return;

        var (message, messageBlocks) = (payload.Message, payload.Message.Blocks.ToList());
        messageBlocks.RemoveAll(b => b is ContextBlock && b.BlockId == "reviewers" || b is ActionBlock && b.BlockId == "pull_request_status");

        messageMetadata.UserProfiles ??= [];

        string displayName;
        if (messageMetadata.UserProfiles.TryGetValue(payload.User.Id, out Profile? profile))
            displayName = profile.DisplayName;
        else
            displayName = (await slackClient.UserInfo(payload.User.Id)).Value.User.Profile.DisplayName;

        string threadMessage, channelMessage;
        if (pullRequestStatus == PullRequestStatus.Close)
        {
            threadMessage = $"{displayName} closed the pull request";
            channelMessage = $"<@{messageMetadata.PullRequestAuthor}>*'s pull request was closed* :x: `{string.Join(" ", messageMetadata.Branches)}`";
        }
        else if (pullRequestStatus == PullRequestStatus.Merge)
        {
            threadMessage = $"{displayName} merged the pull request";
            channelMessage = $"<@{messageMetadata.PullRequestAuthor}>*'s pull request merged* :checkered_flag: `{string.Join(" ", messageMetadata.Branches)}`";
        }
        else
            return;

        messageBlocks.Where(b => b is SectionBlock && b.BlockId == "sectionBlockOnlyMrkdwn").Cast<SectionBlock>().Single().Text.Text = channelMessage;
        payload.Message.Blocks = messageBlocks;

        await slackClient.ChatPostMessage(new(payload.Channel.Id, threadMessage) { ThreadTimestamp = payload.Message.Timestamp });
        await slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = null });
    }
}
