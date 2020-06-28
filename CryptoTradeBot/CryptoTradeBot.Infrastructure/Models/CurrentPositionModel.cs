using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class CurrentPositionModel
    {
        public CurrentPositionModel()
        {
            IntermediateResults = new List<PositionPnlModel>();
        }

        public OrderDirection OrderDirection { get; set; }
        public OrderType OrderType { get; set; }
        public decimal QuoteAssetAmountBefore { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal? StoplossPrice { get; set; }
        public decimal? TakeprofitPrice { get; set; }
        public int PositionDurationInBars { get; set; }
        public int PositionOpenBarIndex { get; set; }
        public GeneralBarModel PositionOpenBar { get; set; }
        public List<PositionPnlModel> IntermediateResults { get; set; }
    }
}
