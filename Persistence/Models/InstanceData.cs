namespace SlackBotManager.Persistence.Models
{
    public record InstanceData(string? EnterpriseId, string? TeamId, bool IsEnterpriseInstall)
    {
        public const string HttpContextKey = "InstanceData";
    }
}
