using System.ComponentModel;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Slack.Models.Elements;
using System.Text;

namespace API.VersionStrategists;

public class ParseLoginPageVersionStrategist : VersionStrategistBase
{
    override public string Name => "parse_login";
    override public string Description => "Parse Login Page for Version";

    override async public Task<string> GetVersion(Dictionary<string, string> values)
    {
        var web = new HtmlWeb();
        var doc = await web.LoadFromWebAsync(values[nameof(LoginPageUrl).ToLower()]);

        var versionLabel = doc.DocumentNode.SelectSingleNode(values[nameof(XPath).ToLower()]);

        if (versionLabel != null)
        {
            string version = versionLabel.InnerText.Trim();

            string newVersion = Regex.Replace(version, values[nameof(ExtractPattern).ToLower()], match =>
            {
                var result = new StringBuilder(values[nameof(FormatPattern).ToLower()]);

                foreach (var groupName in match.Groups.Keys)
                {
                    if (groupName == "0") continue;

                    string value = match.Groups[groupName].Value;

                    if (groupName == "build" && int.TryParse(value, out int buildNumber))
                    {
                        value = buildNumber.ToString($"D{value.Length}");
                    }

                    result = result.Replace($"{{{groupName}}}", value);
                }

                return result.ToString();
            });

            return newVersion;
        }

        return "";
    }

    [Description("Specify the login page URL.")]
    [SlackElementDefinition(InputElementType = typeof(UrlInput))]
    public string? LoginPageUrl { get; set; }
    
    [Description("Specify the XPath to locate the element containing the version value.")]
    [SlackElementDefinition(InitialValue = @"//*[@id=""lblVersion""]")]
    public string? XPath { get; set; }
    
    [Description(@"Specify the regex pattern to extract the build number.
The value of the named group ""build"" will be incremented if possible.")]
    [SlackElementDefinition(InitialValue = @"^(?<coreVersion>\d+\.\d+\.\d+)\s(?<build>\d+)$")]
    public string? ExtractPattern { get; set; }
    [Description(@"Specify the string format with named groups to generate the version string.
Named groups from the regex can be used as placeholders.")]
    [SlackElementDefinition(InitialValue = @"{coreVersion}.{build}")]
    public string? FormatPattern { get; set; }
}
