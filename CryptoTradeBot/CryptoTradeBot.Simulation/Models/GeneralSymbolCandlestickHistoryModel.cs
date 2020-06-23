using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Simulation.Models
{
    public class GeneralSymbolCandlestickHistoryModel
    {
        public GeneralSymbolCandlestickHistoryModel()
        {
            Candles = new List<GeneralCandlestickModel>();
        }

        public string Symbol { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string CandlestickInterval { get; set; }
        public List<GeneralCandlestickModel> Candles { get; set; }
    }
}
