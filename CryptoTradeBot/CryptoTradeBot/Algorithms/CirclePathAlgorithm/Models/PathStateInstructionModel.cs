using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models
{
    public class PathStateInstructionModel
    {
        public bool IsStart { get; set; }
        public bool IsEnd { get; set; }
        public string State { get; set; }
        public string NextState { get; set; }
        public StateTransitionModel Transition { get; set; }
        public SymbolAction Action { get; set; }
    }
}
