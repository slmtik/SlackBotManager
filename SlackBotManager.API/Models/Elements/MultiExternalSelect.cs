using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class MultiExternalSelect : IElement
{
    public IEnumerable<Option<PlainText>>? InitialOptions { get; set; }
    public int? MaxSelectedItems { get; set; }
    public string? ActionId { get; set; }
    public int? MinQueryLength { get; set; }

}
