﻿using Slack.Interfaces;

namespace Slack.Models.Views;

public class HomeView(IEnumerable<IBlock> blocks) : IView
{
    public string Type { get; } = "home";
    public IEnumerable<IBlock> Blocks { get; set; } = blocks;
    public string? CallbackId { get; set; }
    public string? PrivateMetadata { get; set; }
}
