using SlackBotManager.API.Interfaces;

namespace SlackBotManager.API.Models.Elements;

public class Image : IElement
{
    public string AltText { get; set; }
    public string ImageUrl { get; set; }

    public Image(string altText, string imageUrl)
    {
        AltText = altText;
        ImageUrl = imageUrl;
    }
}
