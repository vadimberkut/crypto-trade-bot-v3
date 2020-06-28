using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.StrategyRunner.Enums
{
    public enum StrategyRunnerStatus
    {
        None = 0,
        NoOpenedPosition = 1,
        WaitingPositionOpening = 2,
        OpenedPosition = 3,
    }
}
