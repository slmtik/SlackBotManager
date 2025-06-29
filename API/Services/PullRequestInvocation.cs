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
using System.Text;
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

    private class MessageMetadata(string authorId, Dictionary<string, string> branches, Dictionary<string, string> issues,
        IList<string> tags, bool updateVersion)
    {
        public string AuthorId { get; set; } = authorId;
        public Dictionary<string, string> Branches { get; set; } = branches;
        public Dictionary<string, string> Issues { get; set; } = issues;
        public IList<string> Tags { get; } = tags;
        public bool UpdateVersion { get; } = updateVersion;
        public List<string> Reviewing { get; set; } = [];
        public List<string> Approved { get; set; } = [];
        public Dictionary<string, Profile> UserProfiles { get; set; } = [];

        public static explicit operator JsonObject(MessageMetadata messageMetadata) => new()
        {
            ["author_id"] = messageMetadata.AuthorId,
            ["branches"] = JsonValue.Create(messageMetadata.Branches),
            ["issues"] = JsonValue.Create(messageMetadata.Issues),
            ["tags"] = JsonValue.Create(messageMetadata.Tags),
            ["reviewing"] = JsonValue.Create(messageMetadata.Reviewing),
            ["approved"] = JsonValue.Create(messageMetadata.Approved),
            ["user_profiles"] = JsonValue.Create(messageMetadata.UserProfiles),
            ["update_version"] = JsonValue.Create(messageMetadata.UpdateVersion)
        };

        public static explicit operator MessageMetadata(JsonObject json) => new(
            json["author_id"]?.ToString() ?? throw new ArgumentNullException(nameof(json)),
            json["branches"]?.Deserialize<Dictionary<string, string>>() ?? throw new ArgumentNullException(nameof(json)),
            json["issues"]?.Deserialize<Dictionary<string, string>>() ?? throw new ArgumentNullException(nameof(json)),
            json["tags"]?.Deserialize<IList<string>>() ?? throw new ArgumentNullException(nameof(json)),
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

        var deleteResult = await _slackClient.ChatDeleteMessage(viewMetadata.ChannelId, viewMetadata.Timestamp);
        if (!deleteResult.IsSuccessful)
        {
            return deleteResult;
        }

        var pullRequests = viewMetadata.Branches
            .Select((branch, index) =>
            {
                var uis = (UrlInputState)payload.View.State.Values[$"pull_request_{index}"]["url_input"];
                return new { Branch = branch, Link = $"<{uis.Value}|Pull Request>" };
            })
            .ToDictionary(b => b.Branch, b => b.Link);

        var issues = Enumerable.Range(0, viewMetadata.IssuesNumber)
            .Select(i => (UrlInputState)payload.View.State.Values[$"issue_tracker_{i}"]["url_input"])
            .Select(uis =>
            {
                Uri.TryCreate(uis.Value, UriKind.Absolute, out Uri? issueUri);
                string issueKey = issueUri?.Segments.Last() ?? "Unknown issue";
                return new { IssueKey = issueKey, Link = $"{uis.Value}" };
            })
            .ToDictionary(i => i.IssueKey, i => i.Link);

        IList<string> tags = [];
        if (payload.View.State.Values["tags"]["select"] is MultiStaticSelectState selectedTags && selectedTags.SelectedOptions is not null)
        {
            tags = [.. selectedTags.SelectedOptions.Select(o => o.Value)];
        }

        var updateVersion = payload.View.State.Values["update_version"]["select"] is StaticSelectState { SelectedOption.Value: "yes" };

        var messageMetadata = new MessageMetadata(payload.User.Id, pullRequests, issues, tags, updateVersion);

        var postMessageResult = await _slackClient.ChatPostMessage(new ChatPostMessageRequest(viewMetadata.ChannelId, BuildPullRequestMessage(messageMetadata))
        {
            UnfurlLinks = true,
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = (JsonObject)messageMetadata
            }
        });

        if (postMessageResult.IsSuccessful)
            await _queueStateManager.FinishCreation(payload.User.Id, postMessageResult.Value!.Timestamp, viewMetadata.Branches);

        return postMessageResult;
    }

    private static List<IBlock> BuildPullRequestMessage(MessageMetadata messageMetadata)
    {
        var linkSectionBlock = Enumerable.Range(0, Math.Max(messageMetadata.Branches.Count, messageMetadata.Issues.Count))
            .SelectMany(index => new[] { messageMetadata.Branches.ElementAtOrDefault(index).Value ?? " ", messageMetadata.Issues.ElementAtOrDefault(index).Value ?? " " })
            .Select(link => new MarkdownText(link))
            .Chunk(10)
            .Select(chunk => new SectionBlock(chunk));

        var buttons = new List<Button> { new("Review :eyes:") { ActionId = "review" } };

        if (messageMetadata.Approved.Count < 1)
            buttons.Add(new Button("Approve :white_check_mark:") { ActionId = "approve" });

        buttons.Add(new Button("Merge :checkered_flag:")
        {
            ActionId = "merge",
            Style = "primary",
            Confirm = new("Merge the pull request", "You are about to merge the pull request. Do you wish to proceed?", "Merge", "Cancel")
        });
        buttons.Add(new Button("Close :x:")
        {
            ActionId = "close",
            Style = "danger",
            Confirm = new("Close the pull request", "You are about to close the pull request. Do you wish to proceed?", "Close", "Cancel")
            {
                Style = "danger"
            }
        });

        List<IBlock> blocks =
        [
            new SectionBlock(new MarkdownText($"<@{messageMetadata.AuthorId}> *has created a new pull request* `{string.Join(" ", messageMetadata.Branches.Keys)}`"))
            {
                BlockId = "sectionBlockOnlyMrkdwn"
            },
            new DividerBlock(),
            .. linkSectionBlock,
            new ActionBlock(buttons)
            {
                BlockId = "pull_request_status"
            },
        ];

        if (messageMetadata.Tags.Count > 0)
        {
            blocks.Add(new ContextBlock(messageMetadata.Tags.Select(t => new PlainText(t))));
        }

        if (messageMetadata.Reviewing.Count > 0)
        {
            var reviewersBlocks = new List<IContextElement>() { new PlainText("Reviewing:") };
            foreach (var item in messageMetadata.Reviewing)
                reviewersBlocks.Add(new Image(messageMetadata.UserProfiles[item].DisplayName, messageMetadata.UserProfiles[item].Image_24));

            if (messageMetadata.Approved.Count > 0)
            {
                reviewersBlocks.Add(new PlainText("Approved:"));
                foreach (var item in messageMetadata.Approved)
                    reviewersBlocks.Add(new Image(messageMetadata.UserProfiles[item].DisplayName, messageMetadata.UserProfiles[item].Image_24));
            }

            blocks.Add(new ContextBlock(reviewersBlocks) { BlockId = "reviewers" });
        }

        return blocks;
    }

    private async Task<IRequestResult> SubmitManageDetailsView(ViewSubmissionPayload payload)
    {
        ViewMetadata viewMetadata = JsonSerializer.Deserialize<ViewMetadata>(payload.View.PrivateMetadata)!;

        if (payload.View.State.Values["branches"]["multi_static_select"] is MultiStaticSelectState multiStaticSelectState)
            viewMetadata.Branches = [..(multiStaticSelectState.SelectedOptions ?? []).Select(so => so.Value)];

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
        var messageMetadata = (MessageMetadata)payload.Message.Metadata.EventPayload;
        if (!await UpdateReviewers(messageMetadata, pullRequestStatus, payload.User.Id))
            return RequestResult.Success();

        await _slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, BuildPullRequestMessage(messageMetadata)) 
        {
            Metadata = new()
            {
                EventType = "pull_request_message_created",
                EventPayload = (JsonObject)messageMetadata
            }
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

    private async Task<bool> UpdateReviewers(MessageMetadata messageMetadata, string pullRequestStatus, string currentUserId)
    {
        if (pullRequestStatus.Equals("review") && !messageMetadata.Reviewing.Contains(currentUserId))
            messageMetadata.Reviewing.Add(currentUserId);
        else if (pullRequestStatus.Equals("approve") && !messageMetadata.Approved.Contains(currentUserId)
            && messageMetadata.Reviewing.Contains(currentUserId))
            messageMetadata.Approved.Add(currentUserId);
#if DEBUG
        else if (pullRequestStatus.Equals("review") && messageMetadata.AuthorId.Equals(currentUserId))
        {
            return false;
        }
#endif
        else
        { 
            return false; 
        }

        if (!messageMetadata.UserProfiles.ContainsKey(currentUserId))
            messageMetadata.UserProfiles[currentUserId] = (await _slackClient.UserInfo(currentUserId)).Value.User.Profile;

        return true;
    }

    private async Task<IRequestResult> FinishPullRequest(BlockActionsPayload payload)
    {
        var messageMetadata = (MessageMetadata)payload.Message.Metadata.EventPayload!;
        messageMetadata.Reviewing ??= [];

        var pullRequestStatus = payload.Actions.First().ActionId;
        var currentUserId = payload.User.Id;

        if (!messageMetadata.Reviewing.Contains(currentUserId) && !(pullRequestStatus.Equals("close")
            && messageMetadata.AuthorId.Equals(currentUserId)))
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

        string channelMessage;
        StringBuilder threadMessageBuilder = new();

        if (pullRequestStatus.Equals("close"))
        {
            threadMessageBuilder .AppendLine($"{displayName} closed the pull request");
            channelMessage = $"<@{messageMetadata.AuthorId}>*'s pull request was closed* :x: `{string.Join(" ", messageMetadata.Branches.Keys)}`";
        }
        else if (pullRequestStatus.Equals("merge"))
        {
            threadMessageBuilder.AppendLine($"{displayName} merged the pull request");
            channelMessage = $"<@{messageMetadata.AuthorId}>*'s pull request merged* :checkered_flag: `{string.Join(" ", messageMetadata.Branches.Keys)}`";
        }
        else
        {
            return RequestResult.Success();
        }

        messageBlocks.Where(b => b is SectionBlock && b.BlockId == "sectionBlockOnlyMrkdwn").Cast<SectionBlock>().Single().Text.Text = channelMessage;
        payload.Message.Blocks = messageBlocks;

        if (messageMetadata.UpdateVersion && pullRequestStatus.Equals("merge"))
        {
            var versionUpdateResult = await _webhookSender.SendMessage(messageMetadata.Branches.Keys, messageMetadata.Issues.Keys);
            if(versionUpdateResult.IsSuccessful)
            {
                var versions = versionUpdateResult.Value ?? [];
                threadMessageBuilder.AppendLine(versions.Count > 0
                    ? $"The issue(s) will be updated with the following versions: {string.Join(", ", versions)}"
                    : "No version was generated.");

            }
        }

        await _queueStateManager.FinishReview(payload.Message.Timestamp, messageMetadata.Branches.Keys);
        await _slackClient.ChatPostMessage(new(payload.Channel.Id, threadMessageBuilder.ToString()) { ThreadTimestamp = payload.Message.Timestamp });
        await _slackClient.ChatUpdateMessage(new(payload.Channel.Id, payload.Message.Timestamp, payload.Message.Blocks) { Metadata = null });

        return RequestResult.Success();
    }
}
