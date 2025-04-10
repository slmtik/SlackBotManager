﻿namespace SlackBotManager.Slack.Elements;

public class MultiSelectConversations : IElement
{
    public string? ActionId { get; set; }
    public string[]? InitialConversations { get; set; }
    public PlainText? Placeholder { get; set; }

    public Filter? Filter { get; set; }
}
