using Slack.Interfaces;
using API.Interfaces.Invocations;
using Slack.Models.Events;
using Slack.Models.Commands;
using Slack.Models.Payloads;
using Core.ApiClient;
using System.Text.Json.Nodes;
using Persistence.Interfaces;
using Persistence.Models;
using Microsoft.Extensions.Hosting;

namespace API.Services;

public class SlackManager
{
    private readonly ILogger<SlackManager> _logger;
    private readonly IInstallationStore _installationStore;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly Dictionary<string, Func<Command, Task<IRequestResult>>> _commands = [];
    private readonly Dictionary<string, Func<ViewSubmissionPayload, Task<IRequestResult>>> _viewSubmissionInteractions = [];
    private readonly Dictionary<string, Func<ViewClosedPayload, Task<IRequestResult>>> _viewClosedInteractions = [];
    private readonly Dictionary<(string? BlockId, string? ActionId), Func<BlockActionsPayload, Task<IRequestResult>>> _blockActionsInteractions = [];
    private readonly Dictionary<string, Func<EventPayload, Task<IRequestResult>>> _events = [];

    public SlackManager(IEnumerable<IInvocation> invocations,
                        ILogger<SlackManager> logger,
                        IInstallationStore installationStore,
                        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _installationStore = installationStore;
        _hostEnvironment = hostEnvironment;
        AddInvocation(invocations);
    }

    private void AddInvocation(IEnumerable<IInvocation> invocations)
    {
        foreach (var invocation in invocations)
        {
            if (invocation is ICommandInvocation commandInteraction)
                AddBindings(_commands, commandInteraction.CommandBindings);

            if (invocation is IViewSubmissionInvocation viewSubmissionInteraction)
                AddBindings(_viewSubmissionInteractions, viewSubmissionInteraction.ViewSubmissionBindings);

            if (invocation is IViewClosedInvocation viewClosedInteraction)
                AddBindings(_viewClosedInteractions, viewClosedInteraction.ViewClosedBindings);

            if (invocation is IBlockActionsInvocation blockActionsInteraction)
                AddBindings(_blockActionsInteractions, blockActionsInteraction.BlockActionsBindings);

            if (invocation is IEventInvocation eventInvocation)
                AddBindings(_events, eventInvocation.EventBindings);
        }
    }

    private static void AddBindings<TKey, TValue>(Dictionary<TKey, TValue> target, IDictionary<TKey, TValue> bindings) where TKey : notnull
    {
        foreach (var binding in bindings)
        {
            target[binding.Key] = binding.Value;
        }
    }

    private Task<IRequestResult> HandleInteraction<TKey, TPayload>(TKey key, IDictionary<TKey, Func<TPayload, Task<IRequestResult>>> handlers,
        TPayload payload, string interactionType, System.Action? customLogger = null)
    {
        if (handlers.TryGetValue(key, out var handler))
            return handler.Invoke(payload);

        if (customLogger != null)
        {
            customLogger.Invoke();
        }
        else
        {
            _logger.LogWarning($"The requested {interactionType.ToLower()} interaction is not handled yet. ({interactionType}: {{Key}})", key);
        }

        return Task.FromResult<IRequestResult>(RequestResult.Failure($"The requested {interactionType} interaction is not handled yet."));
    }

    public Task<IRequestResult> HandleCommand(Command slackCommand)
    {
        string commandPrefix = "/";
        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging())
            commandPrefix = $"/{(_hostEnvironment.IsDevelopment() ? "dev" : "stage")}_";

        slackCommand.CommandText = $"/{slackCommand.CommandText[commandPrefix.Length..]}";

        return HandleInteraction(slackCommand.CommandText, _commands, slackCommand, "Command");
    }

    public async Task<IRequestResult> HandleEventPayload(EventPayload payload)
    {
        if (await EventShouldBeSkipped(payload))
            return RequestResult.Success();

        return await HandleInteraction(payload.Event.Type, _events, payload, "Event");
    }

    private async Task<bool> EventShouldBeSkipped(EventPayload payload)
    {
        if (payload.Event is MessageChannelsEvent messageChannelsEvent)
        {
            if(!string.IsNullOrEmpty(messageChannelsEvent.SubType))
                return true;

            if (await _installationStore.Find() is Installation installation && (installation.BotUserId?.Equals(messageChannelsEvent.User) ?? false))
                return true;
        }
        return false;
    }

    public Task<IRequestResult> HandleInteractionPayload(IInteractionPayload payload) =>
        payload switch
        {
            BlockActionsPayload blockActionsPayload => HandleBlockActionsPayload(blockActionsPayload),
            ViewSubmissionPayload viewSubmissionPayload =>
                HandleInteraction(viewSubmissionPayload.View.CallbackId, _viewSubmissionInteractions, viewSubmissionPayload, "view submission",
                    () => _logger.LogWarning("The requested view submission interaction is not handled yet. " +
                                             "(ViewType: {ViewType}, ViewCallbackId: {CallbackId})",
                                             viewSubmissionPayload.View.Type,
                                             viewSubmissionPayload.View.CallbackId)),
            ViewClosedPayload viewClosedPayload => HandleInteraction(viewClosedPayload.View.CallbackId, _viewClosedInteractions, viewClosedPayload, "View close"),
            _ => Task.FromResult<IRequestResult>(RequestResult.Failure("Payload type is not supported or empty"))
        };

    private Task<IRequestResult> HandleBlockActionsPayload(BlockActionsPayload payload)
    {
        var actionKeys = new List<(string? BlockId, string? ActionId)>
        {
            (payload.Actions.First().BlockId, payload.Actions.First().ActionId),
            (payload.Actions.First().BlockId, null),
            (null, payload.Actions.First().ActionId)
        };

        foreach (var actionKey in actionKeys)
        {
            if (_blockActionsInteractions.TryGetValue(actionKey, out var interactionHandler))
            {
                return interactionHandler.Invoke(payload);
            }
        }

        _logger.LogWarning("The requested block action interaction is not handled yet. " +
                           "(ViewType: {ViewType}, ViewCallbackId: {CallbackId}, BlockId: {BlockId}, ActionId: {ActionId})",
                           payload.View.Type,
                           payload.View.CallbackId,
                           payload.Actions.First().BlockId,
                           payload.Actions.First().ActionId);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("The requested block action interaction is not handled yet."));
    }

    public static JsonNode MakeTypePropertyFirstInPayload(JsonNode? payload)
    {
        if (payload is JsonObject jsonObject)
        {
            if (jsonObject.Count > 0 && jsonObject.FirstOrDefault().Key != "type")
            {
                var reorderedJsonObject = new JsonObject();
                if (jsonObject.TryGetPropertyValue("type", out var type))
                {
                    reorderedJsonObject["type"] = type?.DeepClone();
                    jsonObject.Remove("type");
                }
                foreach (var kvp in jsonObject)
                {
                    reorderedJsonObject[kvp.Key] = kvp.Value?.DeepClone();
                }
                return reorderedJsonObject;
            }
        }
        return payload ?? new JsonObject();
    }
}
