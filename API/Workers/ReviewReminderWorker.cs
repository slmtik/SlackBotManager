using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Persistence.Interfaces;
using Persistence.Models;
using Persistence.FileStores;
using Slack;
using API.Services;
using Slack.DTO;
using Core.ApiClient;

namespace API.Workers;

public class ReviewReminderWorker(ILogger<ReviewReminderWorker> logger, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider) : BackgroundService
{
    private record ReviewReminder
    {
        private DateTimeOffset _remindedDateTime;

        public string ReviewMessageTimestamp { get; init; }

        public ReviewReminder(string reviewMessageTimestamp)
        {
            ReviewMessageTimestamp = reviewMessageTimestamp;
            _remindedDateTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reviewMessageTimestamp.Split(".")[0]));
        }

        public bool IsTimePassed(long nextRemindInMinutes)
        {
            return DateTimeOffset.UtcNow > _remindedDateTime.AddMinutes(nextRemindInMinutes);
        }

        public void UpdateLastRemindedTime()
        {
            _remindedDateTime = DateTimeOffset.UtcNow;
        }
    }

    private readonly Dictionary<InstanceData, ReviewReminder> _reminderMessages = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope serviceScope = serviceProvider.CreateScope();
            var queueStateStore = serviceScope.ServiceProvider.GetRequiredService<IQueueStateStore>();
            var slackClient = httpClientFactory.CreateClient();
            slackClient.BaseAddress = new Uri("https://www.slack.com/api/");
            slackClient.Timeout = TimeSpan.FromSeconds(30);

            foreach (var instanceQueue in (await ((FileStoreBase<QueueState>)queueStateStore).FindAll())
                                                 .Where(q => q.ReviewQueue.Count > 0)
                                                 .Select(q => new
                                                 {
                                                     InstanceData = new InstanceData(q.EnterpriseId, q.TeamId, q.IsEnterpriseInstall),
                                                     MessageTimestamp = q.ReviewQueue
                                                                         .SelectMany(x => x.Value)
                                                                         .Where(x => !string.IsNullOrEmpty(x.MessageTimestamp))
                                                                         .Select(x => x.MessageTimestamp)
                                                                         .Min()
                                                 })
                                                 .Where(q => !string.IsNullOrEmpty(q.MessageTimestamp)))
            {
                var setting = (await serviceScope.ServiceProvider.GetRequiredService<ISettingStore>().Find(instanceQueue.InstanceData));
                var reminderSettings = setting?.ReminderSetting;

                var workingTime = DateTimeOffset.UtcNow.TimeOfDay >= TimeSpan.Parse(reminderSettings?.WorkDayStart ?? "00:00")
                        && DateTimeOffset.UtcNow.TimeOfDay <= TimeSpan.Parse(reminderSettings?.WorkDayEnd ?? "00:00");

                if ((!reminderSettings?.Enabled ?? true) || !workingTime) continue;

                var tokenRotator = serviceScope.ServiceProvider.GetRequiredService<SlackTokenRotator>();
                if (await tokenRotator.RotateToken(instanceQueue.InstanceData))
                {
                    var installation = await serviceScope.ServiceProvider.GetRequiredService<IInstallationStore>().Find(instanceQueue.InstanceData);

                    if (_reminderMessages.TryGetValue(instanceQueue.InstanceData, out var reminder))
                    {
                        if (!reminder.ReviewMessageTimestamp.Equals(instanceQueue.MessageTimestamp))
                            _reminderMessages[instanceQueue.InstanceData] = new(instanceQueue.MessageTimestamp!);
                    }
                    else
                    {
                        reminder = new(instanceQueue.MessageTimestamp!);
                        _reminderMessages.Add(instanceQueue.InstanceData, reminder);
                    }

                    if (reminder.IsTimePassed(reminderSettings!.TimeToRemindInMinutes))
                    {
                        var response = await SlackPostMessage(slackClient,
                                                              installation!.BotToken!,
                                                              reminderSettings.RemindingChannelId,
                                                              reminderSettings.MessageTemplate);
                        if (response.IsSuccessful) reminder.UpdateLastRemindedTime();
                    }
                }
            }

            await Task.Delay(60_000, stoppingToken);
        }
    }

    private async Task<IRequestResult> SlackPostMessage(HttpClient slackClient, string botToken, string channelId, string messageText)
    {
        var message = new ChatPostMessageRequest(channelId, messageText);
        string body = JsonSerializer.Serialize(message, SlackClient.ApiJsonSerializerOptions);
        StringContent content = new(body, Encoding.UTF8, "application/json");
        HttpRequestMessage reminderRequest = new(HttpMethod.Post, "chat.postMessage") { Content = content };
        reminderRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", botToken);

        var response = await slackClient.SendAsync(reminderRequest);
        response.EnsureSuccessStatusCode();
        ChatPostMessageResponse result = (await response.Content.ReadFromJsonAsync<ChatPostMessageResponse>(SlackClient.ApiJsonSerializerOptions))!;

        logger?.LogDebug("API response {HttpMethod} {RequestUri}:\n{ResponseMessage}",
            reminderRequest.Method,
            reminderRequest.RequestUri,
            await response.Content.ReadAsStringAsync());

        if (!result.Ok)
        {
            logger?.LogError("Slack API error {HttpMethod} {RequestUri} {SlackError}\n{ResponseMetadata}",
                             reminderRequest.Method,
                             reminderRequest.RequestUri,
                             result.Error,
                             string.Join("\n", result.ResponseMetadata?.Messages ?? []));
            return RequestResult<ChatPostMessageResponse>.Failure(result.Error ?? string.Empty);
        }

        return RequestResult<ChatPostMessageResponse>.Success(result);
    }
}
