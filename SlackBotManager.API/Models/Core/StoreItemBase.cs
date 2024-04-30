namespace SlackBotManager.API.Models.Core;

public abstract record StoreItemBase
{
    public string? EnterpriseId { get; set; }
    public string? TeamId { get; set; }
    public bool? IsEnterpriseInstall { get; set; }
}
