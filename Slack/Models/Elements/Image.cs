﻿namespace SlackBotManager.Slack.Elements;

public class Image : IContextElement
{
    public string AltText { get; set; }
    public string ImageUrl { get; set; }

    public Image(string altText, string imageUrl)
    {
        AltText = altText;
        ImageUrl = imageUrl;
    }
}
