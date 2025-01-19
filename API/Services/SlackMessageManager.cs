using System.Text.Json;
using Persistence.Models;
using Persistence.Interfaces;
using Slack;
using Slack.Interfaces;
using Slack.Models;
using API.Interfaces.Invocations;
using Slack.Models.Events;
using Slack.Models.Commands;
using Slack.Models.Payloads;

namespace API.Services;

public class SlackMessageManager
{
    private readonly SlackClient _client;
    private readonly IInstallationStore _installationStore;
    private readonly ILogger<SlackMessageManager> _logger;

    private readonly Dictionary<string, Func<SlackClient, Command, Task<IRequestResult>>> _commands = [];
    private readonly Dictionary<string, Func<SlackClient, ViewSubmissionPayload, Task<IRequestResult>>> _viewSubmissionInteractions = [];
    private readonly Dictionary<string, Func<SlackClient, ViewClosedPayload, Task<IRequestResult>>> _viewClosedInteractions = [];
    private readonly Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task<IRequestResult>>> _blockActionsInteractions = [];
    private readonly Dictionary<string, Func<SlackClient, EventPayload, Task>> _events = [];

    public SlackMessageManager(SlackClient client,
                               IInstallationStore installationStore,
                               IEnumerable<IInvocation> invocations,
                               ILogger<SlackMessageManager> logger)
    {
        _client = client;
        _installationStore = installationStore;
        _logger = logger;
        AddInvocation(invocations);
    }

    private void AddInvocation(IEnumerable<IInvocation> invocations)
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
        if (_commands.TryGetValue(slackCommand.CommandText, out var commandHandler))
            return _commands[slackCommand.CommandText].Invoke(_client, slackCommand);

        _logger.LogWarning("The requested command is not handled yet. Command: {Command}", slackCommand.CommandText);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("Command is not handled yet"));
    }

    public Task<IRequestResult> HandleInteractionPayload(string payloadString)
    {
        var payload = JsonSerializer.Deserialize<IInteractionPayload>(payloadString, SlackClient.SlackJsonSerializerOptions);

        if (payload is BlockActionsPayload blockActionsPayload)
            return HandleBlockActionsPayload(blockActionsPayload);
        else if (payload is ViewSubmissionPayload viewSubmissionPayload)
            return HandleViewSubmissionPayload(viewSubmissionPayload);
        else if (payload is ViewClosedPayload viewClosedPayload)
            return HandleViewClosedPayload(viewClosedPayload);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("Payload type is not supported or empty"));
    }

    public Task HandleEventPayload(EventPayload payload)
    {
        if (_events.TryGetValue(payload.Event.Type, out var eventHandler))
            return eventHandler.Invoke(_client, payload);

        _logger.LogWarning("The requested event interaction is not handled yet. " +
                           "(EventType: {EventType})",
                           payload.Event.Type);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("The requested event interaction is not handled yet."));
    }

    private Task<IRequestResult> HandleViewSubmissionPayload(ViewSubmissionPayload payload)
    {
        if (_viewSubmissionInteractions.TryGetValue(payload.View.CallbackId, out var bloackActionsInteractionHandler))
            return bloackActionsInteractionHandler.Invoke(_client, payload);

        _logger.LogWarning("The requested view submission interaction is not handled yet. " +
                           "(ViewType: {ViewType}, ViewCallbackId: {CallbackId})",
                           payload.View.Type,
                           payload.View.CallbackId);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("The requested view submission interaction is not handled yet."));
    }

    private Task<IRequestResult> HandleViewClosedPayload(ViewClosedPayload payload) =>
        _viewClosedInteractions[payload.View.CallbackId].Invoke(_client, payload);

    private Task<IRequestResult> HandleBlockActionsPayload(BlockActionsPayload payload)
    {
        if (_blockActionsInteractions.TryGetValue((payload.Actions.First().BlockId, payload.Actions.First().ActionId), out var bloackActionsInteractionHandler))
            return bloackActionsInteractionHandler.Invoke(_client, payload);

        _logger.LogWarning("The requested block action interaction is not handled yet. " +
                           "(ViewType: {ViewType}, ViewCallbackId: {CallbackId}, BlockId: {BlockId}, ActionId: {ActionId})",
                           payload.View.Type,
                           payload.View.CallbackId,
                           payload.Actions.First().BlockId,
                           payload.Actions.First().ActionId);

        return Task.FromResult<IRequestResult>(RequestResult.Failure("The requested block action interaction is not handled yet."));
    }

    public async Task<IRequestResult<Installation>> ProcessOauthCallback(string code)
    {
        var oAuthResult = await _client.OAuthV2Success(new() { Code = code });
        var oAuthData = oAuthResult.Value;

        string? botId = null;
        string? enterpriseUrl = null;
        if (oAuthResult.IsSuccesful && !string.IsNullOrEmpty(oAuthData.AccessToken))
        {
            var authTestResult = await _client.AuthTest(oAuthData.AccessToken);
            botId = authTestResult.Value.BotId;

            if (oAuthData.IsEnterpriseInstall)
                enterpriseUrl = authTestResult.Value.Url;
        }

        var installation = new Installation()
        {
            AppId = oAuthData.AppId,
            EnterpriseId = oAuthData.Enterprise?.Id,
            EnterpriseName = oAuthData.Enterprise?.Name,
            EnterpriseUrl = enterpriseUrl,
            TeamId = oAuthData.Team?.Id,
            TeamName = oAuthData.Team?.Name,
            BotId = botId,
            BotToken = oAuthData.AccessToken,
            BotUserId = oAuthData.BotUserId,
            BotScopes = oAuthData.Scope,
            BotRefreshToken = oAuthData.RefreshToken,
            BotTokenExpiresIn = oAuthData.ExpiresIn,
            UserToken = oAuthData.AuthedUser?.AccessToken,
            UserScopes = oAuthData.AuthedUser?.Scope,
            UserRefreshToken = oAuthData.AuthedUser?.RefreshToken,
            UserTokenExpiresIn = oAuthData.AuthedUser?.ExpiresIn,
            IsEnterpriseInstall = oAuthData.IsEnterpriseInstall,
            TokenType = oAuthData.TokenType
        };
        await _installationStore.Save(installation);

        return RequestResult<Installation>.Success(installation);
    }
}
