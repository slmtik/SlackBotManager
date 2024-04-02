using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class MarkdownTextObject(string text) : IElement, ITextObject
{
    public string Text { get; set; } = text;
}
