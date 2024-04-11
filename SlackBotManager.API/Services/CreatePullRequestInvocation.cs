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
    private class PullRequest(string channelId, string timestampID)
    {
        public string ChannelId { get; set; } = channelId;
        public string TimestampID { get; set; } = timestampID;
        public IEnumerable<string> Branches { get; set; } = [];
        public int IssuesNumber { get; set; }
    }

    private readonly ISettingStore _settingStore;

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

    public CreatePullRequestInvocation(ISettingStore settingStore)
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
        _settingStore = settingStore;
    }

    private async Task<IRequestResult> ShowCreatePullRequestView(SlackClient slackClient, Command commandRequest)
    {
        var setting = _settingStore.FindSetting(commandRequest.EnterpriseId, commandRequest.TeamId, commandRequest.IsEnterpriseInstall);
        if (string.IsNullOrEmpty(setting?.CreatePullRequestChannelId))
            return RequestResult.Failure("Please set the *Channel to post Messages* setting");

        var conversationInfo = await slackClient.ConversationsInfo(setting.CreatePullRequestChannelId);
        if (!conversationInfo.Channel.IsMember)
            return RequestResult.Failure("Please add the App to the channel from the *Channel to post Messages* setting");

        await ShowCreatePullRequestView(slackClient, setting.CreatePullRequestChannelId, commandRequest.UserId, commandRequest.TriggerId);
        return RequestResult.Success();
    }

    public static async Task ShowCreatePullRequestView(SlackClient slackClient, string channelId, string userId, string triggerId)
    {
        var userInfo = await slackClient.UserInfo(userId);

        string message = $"{userInfo.User.Profile.DisplayName} is creating a new pull request";
        var postMessage = await slackClient.ChatPostMessage(new(channelId, message));

        var newPullRequest = new PullRequest(channelId, postMessage.TimeStampId) { Branches = ["develop"], IssuesNumber = 1 };
        await slackClient.ViewOpen(triggerId, BuildCreatePullRequestModal(newPullRequest));
    }

    private static ModalView BuildCreatePullRequestModal(PullRequest pullRequest)
    {
        IEnumerable<Option<PlainText>> tags = [new(new("#usefull"), "#usefull"), new(new("#easy"), "#easy")];

        List<IBlock> blockList = [];

        for (int i = 0; i < pullRequest.Branches.Count(); i++)
            blockList.Add(new InputBlock("Pull Request Link", new UrlInput() { ActionId = "url_input" }) { BlockId = $"pull_request_{i}" });

        for (int i = 0; i < pullRequest.IssuesNumber; i++)
            blockList.Add(new InputBlock("Issue Link", new UrlInput() { ActionId = "url_input" }) { BlockId = $"issue_tracker_{i}" });

        blockList.Add(new InputBlock("Tags", new MultiStaticSelect(tags) { ActionId = "select" }) { BlockId = "tags", Optional = true });
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
            PrivateMetadata = JsonSerializer.Serialize(pullRequest)
        };
    }

    private async Task SubmitCreatePullRequestView(SlackClient slackClient, ViewSubmissionPayload viewSubmissionPayload)
    {
        PullRequest pullRequest = JsonSerializer.Deserialize<PullRequest>(viewSubmissionPayload.View.PrivateMetadata)!;

        await slackClient.ChatDeleteMessage(pullRequest.ChannelId, pullRequest.TimestampID);

        var pullRequestLinks = Enumerable.Range(0, pullRequest.Branches.Count())
            .Select(i => viewSubmissionPayload.View.State.Values[$"pull_request_{i}"]["url_input"])
            .OfType<UrlInputState>()
            .Select(uis => $"<{uis.Value}|Pull Request>")
            .ToList();

        var issueLinks = Enumerable.Range(0, pullRequest.IssuesNumber)
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
                new MarkdownText($"<@{viewSubmissionPayload.User.Id}> *has created a new pull request* `{string.Join(" ", pullRequest.Branches)}`"))
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

        var postMessage = await slackClient.ChatPostMessage(new ChatPostMessageRequest(pullRequest.ChannelId, blocks)
        {
            UnfurlLinks = true,
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = new JsonObject(
                [
                    new("pull_request_author", viewSubmissionPayload.User.Id),
                    new("branches", JsonValue.Create(pullRequest.Branches))
                ])
            }
        });
    }

    private Task SubmitManageDetailsView(SlackClient slackClient, ViewSubmissionPayload viewSubmissionPayload)
    {
        PullRequest pullRequest = JsonSerializer.Deserialize<PullRequest>(viewSubmissionPayload.View.PrivateMetadata)!;

        if (viewSubmissionPayload.View.State.Values["branches"]["multi_static_select"] is MultiStaticSelectState multiStaticSelectState)
            pullRequest.Branches = multiStaticSelectState.SelectedOptions!.Select(so => so.Value).ToList();

        if (viewSubmissionPayload.View.State.Values["issues"]["number_input"] is NumberInputState numberInputState)
            pullRequest.IssuesNumber = int.Parse(numberInputState.Value);

        var viewToCreatePullRequest = BuildCreatePullRequestModal(pullRequest);
        var viewId = viewSubmissionPayload.View.RootViewId;

        return slackClient.ViewUpdate(viewId, viewToCreatePullRequest);
    }

    private Task CloseCreatePullRequestView(SlackClient slackClient, ViewClosedPayload viewClosedPayload)
    {
        PullRequest pullRequest = JsonSerializer.Deserialize<PullRequest>(viewClosedPayload.View.PrivateMetadata)!;
        return slackClient.ChatDeleteMessage(pullRequest.ChannelId, pullRequest.TimestampID);
    }

    private Task ShowManageDetailsView(SlackClient slackClient, BlockActionsPayload payload)
    {
        var (triggerId, privateMetadata) = (payload.TriggerId, payload.View.PrivateMetadata);

        PullRequest pullRequest = JsonSerializer.Deserialize<PullRequest>(privateMetadata)!;

        return slackClient.ViewPush(triggerId, BuildManageDetailsModal(pullRequest));
    }

    private static ModalView BuildManageDetailsModal(PullRequest pullRequest)
    {
        IEnumerable<Option<PlainText>> _branches = [new(new("develop"), "develop"), new(new("release"), "release")];

        return new ModalView("Manage Details", 
        [
            new InputBlock("Branches", new MultiStaticSelect(_branches)
            {
                InitialOptions = _branches.Where(b => pullRequest.Branches.Contains(b.Value)).ToList(),
                ActionId = "multi_static_select"
            }) { BlockId = "branches" },
            new InputBlock("Number of issues in pull request", new NumberInput()
            {
                IsDecimalAllowed = false,
                ActionId = "number_input",
                InitialValue = pullRequest.IssuesNumber.ToString(),
                MinValue = 1.ToString(),
                MaxValue = 5.ToString()
            }) { BlockId = "issues" }
        ])
        {
            CallbackId = "manage_details",
            Submit = new("Submit"),
            PrivateMetadata = JsonSerializer.Serialize(pullRequest)
        };
    }

    private async Task UpdatePullRequestStatus(SlackClient slackClient, BlockActionsPayload payload)
    {
        var pullRequestStatus = (PullRequestStatus)Enum.Parse(typeof(PullRequestStatus), payload.Actions.First().ActionId, true);
        if (!await UpdateReviewers(slackClient, payload.Message!, pullRequestStatus, payload.User.Id))
            return;

        await slackClient.ChatUpdateMessage(new(payload.Channel!.Id,
                                                payload.Message!.TimestampId,
                                                payload.Message.Blocks)
        { Metadata = payload.Message.Metadata });

        string threadMessage;
        if (pullRequestStatus == PullRequestStatus.Review)
            threadMessage = $"<@{payload.User.Id}> started reviewing";
        else if (pullRequestStatus == PullRequestStatus.Approve)
        {
            var displayName = payload.Message.Metadata!.EventPayload["user_profiles"]
                                                       .Deserialize<Dictionary<string, Profile>>()![payload.User.Id].DisplayName;
            threadMessage = $"{displayName} approved the pull request";
        }
        else
            return;

        await slackClient.ChatPostMessage(new(payload.Channel!.Id, threadMessage) { ThreadTimeStampId = payload.Message.TimestampId });
    }

    private async static Task<bool> UpdateReviewers(SlackClient slackClient, Message message, PullRequestStatus pullRequestStatus, string userId)
    {
        var reviewing = message.Metadata!.EventPayload["reviewing"].Deserialize<List<string>>() ?? [];
        var approved = message.Metadata!.EventPayload["approved"].Deserialize<List<string>>() ?? [];

        if (pullRequestStatus == PullRequestStatus.Review && !reviewing.Contains(userId))
            reviewing.Add(userId);
        else if (pullRequestStatus == PullRequestStatus.Approve && !approved.Contains(userId) && reviewing.Contains(userId))
            approved.Add(userId);
        else
            return false;

        var messageBlocks = message.Blocks.ToList();
        messageBlocks.Remove(messageBlocks.Find(b => b is ContextBlock && b.BlockId == "reviewers")!);

        var userProfiles = message.Metadata!.EventPayload["user_profiles"].Deserialize<Dictionary<string, Profile>>(SlackClient.SlackJsonSerializerOptions) ?? [];

        if (!userProfiles.ContainsKey(userId))
            userProfiles[userId] = (await slackClient.UserInfo(userId)).User.Profile;

        var reviewersBlocks = new List<IElement>() { new PlainText("Reviewing:") };
        foreach (var item in reviewing)
            reviewersBlocks.Add(new Image(userProfiles[item].DisplayName, userProfiles[item].Image_24));

        if (approved.Count > 0)
        {
            reviewersBlocks.Add(new PlainText("Approved:"));
            foreach (var item in approved)
                reviewersBlocks.Add(new Image(userProfiles[item].DisplayName, userProfiles[item].Image_24));
        }

        var reviewersBlock = new ContextBlock(reviewersBlocks) { BlockId = "reviewers" };

        messageBlocks.Add(reviewersBlock);
        message.Blocks = messageBlocks;

        message.Metadata.EventPayload["reviewing"] = JsonValue.Create(reviewing);
        message.Metadata.EventPayload["approved"] = JsonValue.Create(approved);
        message.Metadata.EventPayload["user_profiles"] = JsonValue.Create(userProfiles);

        return true;
    }

    private async Task FinishPullRequest(SlackClient slackClient, BlockActionsPayload payload)
    {
        var reviewing = payload.Message!.Metadata!.EventPayload["reviewing"].Deserialize<List<string>>() ?? [];
        var pullRequestStatus = (PullRequestStatus)Enum.Parse(typeof(PullRequestStatus), payload.Actions.First().ActionId, true);
        var pullRequestAuthor = payload.Message.Metadata!.EventPayload["pull_request_author"]!.ToString();
        var currentUserId = payload.User.Id;

        if (!reviewing.Contains(currentUserId) && !(pullRequestStatus == PullRequestStatus.Close && pullRequestAuthor.Equals(currentUserId)))
            return;

        var (message, messageBlocks) = (payload.Message, payload.Message.Blocks.ToList());
        messageBlocks.RemoveAll(b => b is ContextBlock && b.BlockId == "reviewers" || b is ActionBlock && b.BlockId == "pull_request_status");

        var branches = payload.Message.Metadata.EventPayload["branches"].Deserialize<IEnumerable<string>>() ?? [];
        var userProfiles = payload.Message.Metadata.EventPayload["user_profiles"].Deserialize<Dictionary<string, Profile>>(SlackClient.SlackJsonSerializerOptions) ?? [];

        string displayName;
        if (userProfiles.TryGetValue(payload.User.Id, out Profile? profile))
            displayName = profile.DisplayName;
        else
            displayName = (await slackClient.UserInfo(payload.User.Id)).User.Profile.DisplayName;

        string threadMessage, channelMessage;
        if (pullRequestStatus == PullRequestStatus.Close)
        {
            threadMessage = $"{displayName} closed the pull request";
            channelMessage = $"<@{pullRequestAuthor}>*'s pull request was closed* :x: `{string.Join(" ", branches)}`";
        }
        else if (pullRequestStatus == PullRequestStatus.Merge)
        {
            threadMessage = $"{displayName} merged the pull request";
            channelMessage = $"<@{pullRequestAuthor}>*'s pull request merged* :checkered_flag: `{string.Join(" ", branches)}`";
        }
        else
            return;

        messageBlocks.Where(b => b is SectionBlock && b.BlockId == "sectionBlockOnlyMrkdwn").Cast<SectionBlock>().Single().Text!.Text = channelMessage;
        payload.Message.Blocks = messageBlocks;

        await slackClient.ChatPostMessage(new(payload.Channel!.Id, threadMessage) { ThreadTimeStampId = payload.Message.TimestampId });
        await slackClient.ChatUpdateMessage(new(payload.Channel!.Id, payload.Message.TimestampId, payload.Message.Blocks) { Metadata = null });
    }
}
