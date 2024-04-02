namespace SlackBotManager.API.Models.Payloads;

public class View
{
    public string PrivateMetadata { get; set; }
    public State State { get; set; }
    public string RootViewId { get; set; }
    public string ViewCallBackId { get; set; }
    public string CallbackId { get; set; }

}