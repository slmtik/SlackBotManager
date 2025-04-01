using API.Interfaces.Invocations;
using Core.ApiClient;
using Persistence.Interfaces;
using Slack.Models.Elements;
using Slack.Models.Events;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace API.VersionStrategists;

public class ChannelMonitorVersionStrategist : VersionStrategistBase, IEventInvocation
{
    private readonly ISettingStore _settingStore;

    public override string Name => "watch_chat";

    public override string Description => "Monitor Channel for Version Updates";

    public Dictionary<string, Func<EventPayload, Task<IRequestResult>>> EventBindings { get; } = [];

    public ChannelMonitorVersionStrategist(ISettingStore settingStore)
    {
        EventBindings.Add("message", WatchChannelMessage);
        _settingStore = settingStore;
    }

    private async Task<IRequestResult> WatchChannelMessage(EventPayload payload)
    {
        if (payload.Event is not MessageChannelsEvent messageChannelsEvent)
            return RequestResult.Failure("The passed payload is not the Message Event Payload");

        var settings = await _settingStore.Find();
        if (settings?.WebhookSetting == null)
            return RequestResult.Failure("Failed to retrieve settings.");

        foreach (var versionStrategy in settings.WebhookSetting.VersionStrategies)
        {
            if (versionStrategy.Value.Name != Name)
                continue;

            if (!versionStrategy.Value.Values.TryGetValue(nameof(ChannelId).ToLower(), out var channelId) 
                || string.IsNullOrWhiteSpace(channelId)
                || channelId != messageChannelsEvent.Channel)
                continue;

            if (!versionStrategy.Value.Values.TryGetValue(nameof(RegexPattern).ToLower(), out var regexPattern) || string.IsNullOrWhiteSpace(regexPattern))
                continue;

            try
            {
                string lastBuildKey = nameof(LastBuildNumber).ToLower();

                bool TryExtractBuildNumber(string? text, out int buildNumber)
                {
                    buildNumber = 0;
                    var match = Regex.Match(text ?? string.Empty, regexPattern);
                    return match.Success && int.TryParse(match.Groups[1].Value, out buildNumber);
                }

                if (TryExtractBuildNumber(messageChannelsEvent.Text, out var buildNumber)
                    || TryExtractBuildNumber(messageChannelsEvent.Attachments?.FirstOrDefault()?.Text, out buildNumber))
                {
                    if (versionStrategy.Value.Values.TryGetValue(lastBuildKey, out var lastBuild)
                        && int.TryParse(lastBuild, out var lastBuildNumber)
                        && buildNumber > lastBuildNumber)
                    {
                        versionStrategy.Value.Values[lastBuildKey] = buildNumber.ToString();
                        await _settingStore.Save(settings);
                    }
                }
            }
            catch (ArgumentException)
            {
                return RequestResult.Failure("Invalid regex pattern in version strategy.");
            }
        }

        return RequestResult.Success();
    }

    public override Task<string> GetVersion(Dictionary<string, string> values)
    {
        if (values.TryGetValue(nameof(CoreVersion).ToLower(), out var coreVersion)
            && values.TryGetValue(nameof(LastBuildNumber).ToLower(), out var lastBuild)
            && int.TryParse(lastBuild, out var lastBuildNumber))
        {
            return Task.FromResult($"{coreVersion}.{++lastBuildNumber}");
        }

        return Task.FromResult("");
    }

    [Description("Select a channel to monitor for messages.")]
    [SlackElementDefinition(InputElementType = typeof(SelectPublicChannel))]
    public string? ChannelId { get; set; }

    [Description(@"Regex pattern to extract the build number from the message.")]
    [SlackElementDefinition(InitialValue = "Build #0.0.0.(\\d{1,4})")]
    public string? RegexPattern { get; set; }

    [Description(@"The core version associated with the branch. 
This version will be concatenated with the build number.")]
    public string? CoreVersion { get; set; }

    [Description(@"The last recorded build number.  
The version to be sent will be incremented from this value.")]
    [SlackElementDefinition(InputElementType = typeof(NumberInput))]
    public int? LastBuildNumber { get; set; }
}