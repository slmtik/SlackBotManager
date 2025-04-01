using API.Interfaces;
using Core.ApiClient;
using Persistence.Interfaces;
using Persistence.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace API.Services;

public class WebhookSender(IHttpClientFactory httpClientFactory, ISettingStore settingStore, IVersionStrategistResolver versionStrategistResolver)
{
    private const string _versionsVariable = "%Versions%";
    private const string _issuesVariable = "%Issues%";

    public async Task<IRequestResult<ICollection<string>>> SendMessage(IEnumerable<string> branches, IEnumerable<string> issues)
    {
        var settings = await settingStore.Find();

        if (settings?.WebhookSetting is not WebhookSetting webhookSetting)
        {
            return RequestResult<ICollection<string>>.Failure("Webhook settings are not configured.");
        }

        var getVersionTasks = branches.Select(async branch =>
        {
            if (!webhookSetting.VersionStrategies.TryGetValue(branch, out var strategy))
            {
                return null;
            }
            
            var strategist = versionStrategistResolver.GetStrategist(strategy.Name);
            return await strategist.GetVersion(strategy.Values);
        });

        var versions = (await Task.WhenAll(getVersionTasks)).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToList();
        if (versions.Count > 0)
        {
            var templateBuilder = new StringBuilder(webhookSetting.MessageTemplate);
            ReplaceVariable(templateBuilder, _versionsVariable, versions);
            ReplaceVariable(templateBuilder, _issuesVariable, issues);

            var httpClient = httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhookSetting.WebhookUrl)
            {
                Content = new StringContent(templateBuilder.ToString(), new MediaTypeHeaderValue("application/json"))
            };

            requestMessage.Headers.Add(webhookSetting.WebhookHeader, webhookSetting.WebhookSecret);

            var response = await httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
        }

        return RequestResult<ICollection<string>>.Success(versions);
    }

    private static void ReplaceVariable(StringBuilder templateBuilder, string variable, IEnumerable<string> values)
    {
        string jsonArrayString;
        string valueString;
        if (values.Any())
        {
            jsonArrayString = JsonSerializer.Serialize(values);
            valueString = $"\"{string.Join(", ", values)}\"";
        }
        else
        {
            jsonArrayString = "[]";
            valueString = "\"\"";
        }

        templateBuilder.Replace($"[{variable}]", jsonArrayString);
        templateBuilder.Replace(variable, valueString);
    }
}
