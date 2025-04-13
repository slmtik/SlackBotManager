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
using Core;

namespace API.Workers;

public class ReviewReminderWorker(ILogger<ReviewReminderWorker> logger, IServiceProvider serviceProvider) : BackgroundService
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
            var slackClient = serviceScope.ServiceProvider.GetRequiredService<SlackClient>();

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
                        slackClient.AuthToken = installation.BotToken;
                        var response = await slackClient.ChatPostMessage(new(reminderSettings.RemindingChannelId, reminderSettings.MessageTemplate));
                        if (response.IsSuccessful) reminder.UpdateLastRemindedTime();
                    }
                }
            }

            await Task.Delay(60_000, stoppingToken);
        }
    }
}
