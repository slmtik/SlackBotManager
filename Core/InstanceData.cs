using System.Net;
using System.Text.Json.Nodes;

namespace Core;

public record InstanceData(string? EnterpriseId, string? TeamId, bool? IsEnterpriseInstall)
{
    public static InstanceData Parse(string body)
    {
        string? enterpriseId = null;
        string? teamId = null;
        bool isEnterpriseInstall;

        JsonNode? jsonBody = null;

        if (body.StartsWith('{'))
        {
            var json = JsonNode.Parse(body);
            if (json!["authorizations"] is JsonArray authorizations && authorizations.Count > 0)
                jsonBody = authorizations[0];
            else
                jsonBody = json;
        }
        else
        {
            var formBody = ParseFormUrlEncoded(body);
            if (formBody.TryGetValue("payload", out var payload))
                jsonBody = JsonNode.Parse(payload!)!;
            else
                jsonBody = new JsonObject(formBody.Select(kvp => KeyValuePair.Create<string, JsonNode>(kvp.Key, kvp.Value.ToString()))!);
        }

        enterpriseId = (jsonBody?["enterprise_id"] ?? jsonBody?["enterprise"]?["id"] ?? jsonBody?["enterprise"])?.ToString();
        teamId = (jsonBody?["team_id"] ?? jsonBody?["team"]?["id"] ?? jsonBody?["team"] ?? jsonBody?["user"]?["team_id"])?.ToString();
        isEnterpriseInstall = Convert.ToBoolean(jsonBody?["is_enterprise_install"]?.ToString());

        return new(enterpriseId, teamId, isEnterpriseInstall);
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string formData)
    {
        var result = new Dictionary<string, string>();
        var pairs = formData.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]);
            var value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : "";
            result[key] = value;
        }

        return result;
    }
}
