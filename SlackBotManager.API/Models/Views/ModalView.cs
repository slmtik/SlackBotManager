using SlackBotManager.API.Interfaces;
using SlackBotManager.API.Models.Elements;

namespace SlackBotManager.API.Models.Surfaces;

public class ModalView(PlainTextObject title, IEnumerable<IBlock> blocks) : IView
{
    public string Type { get; } = "modal";
    public PlainTextObject Title { get; set; } = title;
    public IEnumerable<IBlock> Blocks { get; set; } = blocks;
    public string? CallbackId { get; set; }
    public PlainTextObject? Submit { get; set; }
    public string? PrivateMetadata { get; set; }
    public bool NotifyOnClose { get; set; } = false;

    public ModalView(string title, IEnumerable<IBlock> blocks) : this(new PlainTextObject(title), blocks)
    {
        
    }
}
