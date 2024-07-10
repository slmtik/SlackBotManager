﻿namespace SlackBotManager.Slack.Elements;

public class PlainText(string text) : IContextElement, ITextObject
{
    public string Type { get; } = "plain_text";
    public string Text { get; set; } = text;
}
