using SlackBotManager.API.Models.SlackClient;
using System.Text.Json;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Strategies;

namespace SlackBotManager.API.Services;

public class SlackMessageManager
{
    private readonly string _channelId;
    private readonly SlackClient _slackClient;

    private readonly Dictionary<string, Func<SlackClient, CommandRequest, Task>> _commands = [];
    private readonly Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task>> _viewSubmissionInteractions = [];
    private readonly Dictionary<string, Func<SlackClient, ViewClosedPayload, Task>> _viewClosedInteractions = [];
    private readonly Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> _blockActionsInteractions = [];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SlackMessageManager(SlackClient slackClient, IConfiguration configuration)
    {
        _channelId = configuration["Slack:SLACK_CHANNEL_ID"]
            ?? throw new ArgumentException("The slack channel is not set in the configuration", nameof(configuration));
        _slackClient = slackClient;

        AddInteraction(new CreatePullRequestInteraction());
    }

    public void AddInteraction(IInteraction strategy)
    {
        if (strategy is ISlashCommand slashCommand)
            foreach (var binding in slashCommand.CommandBindings)
                _commands.Add(binding.Key, binding.Value);

        if (strategy is IViewSubmissionInteraction viewSubmissionInteraction)
            foreach (var binding in viewSubmissionInteraction.ViewSubmissionBindings)
                _viewSubmissionInteractions.Add(binding.Key, binding.Value);

        if (strategy is IViewClosedInteraction viewClosedInteraction)
            foreach (var binding in viewClosedInteraction.ViewClosedBindings)
                _viewClosedInteractions.Add(binding.Key, binding.Value);

        if (strategy is IBlockActionsInteraction blockActionsInteraction)
            foreach (var binding in blockActionsInteraction.BlockActionsBindings)
                _blockActionsInteractions.Add(binding.Key, binding.Value);
    }

    public Task HandleCommand(CommandRequest slackCommand)
    {
        if (slackCommand.ChannelId == _channelId)
            return _commands[slackCommand.Command].Invoke(_slackClient, slackCommand);
        return Task.CompletedTask;
    }

    public async Task HandlePayload(string payloadString)
    {
        var payload = JsonSerializer.Deserialize<IPayload>(payloadString, _jsonSerializerOptions);
        if (payload == null)
            return;

        if (payload is BlockActionsPayload blockActionsPayload)
            await HandleBlockActionsPayload(blockActionsPayload);
        else if (payload is ViewSubmissionPayload viewSubmissionPayload)
            await HandleViewSubmissionPayload(viewSubmissionPayload);
        else if (payload is ViewClosedPayload viewClosedPayload)
            await HandleViewClosedPayload(viewClosedPayload);
    }

    private Task HandleViewSubmissionPayload(ViewSubmissionPayload viewSubmissionPayload) =>
        _viewSubmissionInteractions[viewSubmissionPayload.View.CallbackId].Invoke(_slackClient, viewSubmissionPayload);

    private Task HandleViewClosedPayload(ViewClosedPayload viewClosedPayload) =>
        _viewClosedInteractions[viewClosedPayload.View.CallbackId].Invoke(_slackClient, viewClosedPayload);

    private Task HandleBlockActionsPayload(BlockActionsPayload payload) =>
        _blockActionsInteractions[(payload.Actions.First().BlockId, payload.Actions.First().ActionId)].Invoke(_slackClient, payload);
}
