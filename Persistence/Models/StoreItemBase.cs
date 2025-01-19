namespace Persistence.Models;

public abstract record StoreItemBase
{
    public string? EnterpriseId { get; set; }
    public string? TeamId { get; set; }
    public bool? IsEnterpriseInstall { get; set; }
}
