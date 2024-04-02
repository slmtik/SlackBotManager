﻿using SlackBotManager.API.Models.Payloads;
using SlackBotManager.API.Services;

namespace SlackBotManager.API.Interfaces
{
    public interface IBlockActionsInteraction
    {
        public Dictionary<(string BlockId, string ActionId), Func<SlackClient, BlockActionsPayload, Task>> BlockActionsBindings { get; }
    }
}
