using Slack;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Slack.Models.Commands;
using Core;
using Persistence.Interfaces;
using Slack.Interfaces;
using Slack.Models.Events;
using System.Text.Json.Nodes;
using Core.ApiClient;

namespace API.Services;

public class SlackSocketModeService(IHttpClientFactory httpClientFactory,
                                    IConfiguration configuration,
                                    ILogger<SlackSocketModeService> logger,
                                    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly string _appToken = configuration["Slack:SocketMode:AppToken"]
        ?? throw new ArgumentException("Socket Mode is enabled, but AppToken is missing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSocketSessionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Slack Socket Mode session. Will retry in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunSocketSessionAsync(CancellationToken cancellationToken)
    {
        var socketUrl = await GetWebSocketUrlAsync(cancellationToken);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {_appToken}");

        await socket.ConnectAsync(new Uri(socketUrl), cancellationToken);
        logger.LogInformation("Connected to Slack via Socket Mode.");

        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var message = Encoding.UTF8.GetString(ms.ToArray());

            JsonNode? root = JsonNode.Parse(message);

            if (root?["type"] is JsonNode type)
            {
                logger.LogInformation("Message type received: {MessageType}", type);

                if (type.ToString() == "disconnect")
                {
                    logger.LogWarning("Received 'disconnect' from Slack. Will reconnect.");
                    break;
                }

                if (root["payload"] is JsonNode payload)
                {
                    using var scope = serviceScopeFactory.CreateScope();

                    var instanceData = InstanceData.Parse(payload.ToString());
                    
                    var slackTokenRotator = scope.ServiceProvider.GetRequiredService<SlackTokenRotator>();
                    if (!await slackTokenRotator.RotateToken(instanceData))
                    {
                        logger.LogWarning("Install app first");
                        continue;
                    }

                    var installationStore = scope.ServiceProvider.GetRequiredService<IInstallationStore>();
                    var installation = await installationStore.Find(instanceData.EnterpriseId, instanceData.TeamId, instanceData.IsEnterpriseInstall);

                    var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
                    requestContext.BotToken = installation.BotToken;
                    requestContext.InstanceData = instanceData;

                    Func<JsonNode, SlackManager, Task<IRequestResult>>? handler = type.ToString() switch
                    {
                        "slash_commands" => HandleCommandPayload,
                        "interactive" => HandleInteractivePayload,
                        "events_api" => HandleEventsPayload,
                        _ => null,
                    };

                    var slackManager = scope.ServiceProvider.GetRequiredService<SlackManager>();
                    if (handler == null)
                        logger.LogInformation("Unhandled message type {Type}", type.ToString());
                    else
                    {
                        var requestResult = await handler.Invoke(payload, slackManager);
                        if (!requestResult.IsSuccessful)
                        {
                            logger.LogWarning("Something went wrong. Message Type: {Type}. Error: {Error}", type.ToString(), requestResult.Error);
                        }
                    }
                }
            }

            if (root?["envelope_id"] is JsonNode envelopeId)
            {
                await SendAck(socket, envelopeId, cancellationToken);
            }
        }

        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cancellationToken);
            logger.LogInformation("WebSocket closed cleanly.");
        }
        else
        {
            logger.LogWarning("Slack WebSocket closed with state: {State}", socket.State);
        }
    }

    private static async Task<IRequestResult> HandleCommandPayload(JsonNode payload, SlackManager slackManager)
    {
        var command = JsonSerializer.Deserialize<Command>(payload, SlackClient.ApiJsonSerializerOptions);
        var requestResult = await slackManager.HandleCommand(command);

        if (!requestResult.IsSuccessful) await new HttpClient().PostAsJsonAsync(command.ResponseUrl, new { text = requestResult.Error });

        return requestResult;
    }

    private static async Task<IRequestResult> HandleInteractivePayload(JsonNode payload, SlackManager slackManager)
    {
        var interactionPayload = JsonSerializer.Deserialize<IInteractionPayload>(payload, SlackClient.ApiJsonSerializerOptions);
        return await slackManager.HandleInteractionPayload(interactionPayload);
    }

    private static async Task<IRequestResult> HandleEventsPayload(JsonNode payload, SlackManager slackManager)
    {
        payload["event"] = SlackManager.MakeTypePropertyFirstInPayload(payload["event"]);

        var eventPayload = JsonSerializer.Deserialize<EventPayload>(payload, SlackClient.ApiJsonSerializerOptions);
        return await slackManager.HandleEventPayload(eventPayload);
    }

    private async Task SendAck(ClientWebSocket socket, JsonNode envelopeId, CancellationToken cancellationToken)
    {
        var ack = new { envelope_id = envelopeId.ToString() };
        var ackJson = JsonSerializer.Serialize(ack);
        var ackBytes = Encoding.UTF8.GetBytes(ackJson);

        await socket.SendAsync(new ArraySegment<byte>(ackBytes), WebSocketMessageType.Text, true, cancellationToken);
        logger.LogInformation("Sent ACK.");
    }

    private async Task<string> GetWebSocketUrlAsync(CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _appToken);

        var response = await httpClient.PostAsync("https://slack.com/api/apps.connections.open", null, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new Exception("Failed to open connection: " + content);

        return root.GetProperty("url").GetString()!;
    }
}