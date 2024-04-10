using System.Text.Json;
using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Events;
using SlackBotManager.API.Models.Commands;
using SlackBotManager.API.Models.Core;

namespace SlackBotManager.API.Services;

public class SlackMessageManager
{
    private readonly SlackClient _slackClient;
    private readonly ILogger<SlackMessageManager> _logger;

    private readonly Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> _commands = [];
    private readonly Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task>> _viewSubmissionInteractions = [];
    private readonly Dictionary<string, Func<SlackClient, ViewClosedPayload, Task>> _viewClosedInteractions = [];
    private readonly Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> _blockActionsInteractions = [];
    private readonly Dictionary<string, Func<SlackClient, EventPayload, Task>> _events = [];

    public SlackMessageManager(SlackClient slackClient,
                               ILogger<SlackMessageManager> logger,
                               CreatePullRequestInvocation createPullRequestInvocation,
                               HomeTabInvocation homeTabInvocation)
    {
        _slackClient = slackClient;
        _logger = logger;
        AddInvocation(createPullRequestInvocation, homeTabInvocation);
    }

    private void AddInvocation(params IInvocation[] invocations)
    {
        foreach (var invocation in invocations)
        {
            if (invocation is ICommandInvocation commandInteraction)
                foreach (var binding in commandInteraction.CommandBindings)
                    _commands.Add(binding.Key, binding.Value);

            if (invocation is IViewSubmissionInvocation viewSubmissionInteraction)
                foreach (var binding in viewSubmissionInteraction.ViewSubmissionBindings)
                    _viewSubmissionInteractions.Add(binding.Key, binding.Value);

            if (invocation is IViewClosedInvocation viewClosedInteraction)
                foreach (var binding in viewClosedInteraction.ViewClosedBindings)
                    _viewClosedInteractions.Add(binding.Key, binding.Value);

            if (invocation is IBlockActionsInvocation blockActionsInteraction)
                foreach (var binding in blockActionsInteraction.BlockActionsBindings)
                    _blockActionsInteractions.Add(binding.Key, binding.Value);

            if (invocation is IEventInvocation eventInvocation)
                foreach (var binding in eventInvocation.EventBindings)
                    _events.Add(binding.Key, binding.Value);
        }
    }

    public Task<IRequestResult> HandleCommand(Command slackCommand)
    {
        if(_commands.TryGetValue(slackCommand.CommandText, out var commandHandler))
            return _commands[slackCommand.CommandText].Invoke(_slackClient, slackCommand);

        _logger.LogWarning("The requested command is not handled yet. Command: {Command}", slackCommand.CommandText);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("Command is not handled yet"));
    }

    public Task HandleInteractionPayload(string payloadString)
    {
        var payload = JsonSerializer.Deserialize<IInteractionPayload>(payloadString, SlackClient.SlackJsonSerializerOptions);
        if (payload == null) return Task.CompletedTask;

        if (payload is BlockActionsPayload blockActionsPayload)
            return HandleBlockActionsPayload(blockActionsPayload);
        else if (payload is ViewSubmissionPayload viewSubmissionPayload)
            return HandleViewSubmissionPayload(viewSubmissionPayload);
        else if (payload is ViewClosedPayload viewClosedPayload)
            return HandleViewClosedPayload(viewClosedPayload);

        return Task.CompletedTask;
    }

    public Task HandleEventPayload(EventPayload eventPayload)
    {
        return _events[eventPayload.Event!.Type!].Invoke(_slackClient, eventPayload);
    }

    private Task HandleViewSubmissionPayload(ViewSubmissionPayload viewSubmissionPayload) =>
        _viewSubmissionInteractions[viewSubmissionPayload.View.CallbackId].Invoke(_slackClient, viewSubmissionPayload);

    private Task HandleViewClosedPayload(ViewClosedPayload viewClosedPayload) =>
        _viewClosedInteractions[viewClosedPayload.View.CallbackId].Invoke(_slackClient, viewClosedPayload);

    private Task HandleBlockActionsPayload(BlockActionsPayload payload)
    {
        if(_blockActionsInteractions.TryGetValue((payload.Actions.First().BlockId, payload.Actions.First().ActionId), out var bloackActionsInteractionHandler))
            return bloackActionsInteractionHandler.Invoke(_slackClient, payload);

        _logger.LogWarning("The requested block action interaction is not handled yet. " +
                           "(ViewType: {ViewType}, ViewCallbackId: {CallbackId}, BlockId: {BlockId}, ActionId: {ActionId})",
                           payload.View.Type,
                           payload.View.CallbackId,
                           payload.Actions.First().BlockId,
                           payload.Actions.First().ActionId);

        return Task.CompletedTask;
    }
}
