namespace SlackBotManager.API.Models.Core
{
    public record InstanceData(string? EnterpriseId, string? TeamId, bool IsEnterpriseInstall)
    {
        public const string HttpContextKey = "InstanceData";
    }
}
