using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm.Models
{
    public class StateTransitionModel
    {
        public string Symbol { get; set; }
        public string State1 { get; set; }
        public string State2 { get; set; }
    }
}
