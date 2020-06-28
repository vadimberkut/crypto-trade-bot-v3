using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.StrategyRunner.Models
{
    public class StrategyDefinitionModel
    {
        /// <summary>
        /// How long to wait after open signal was received. When expired - cancel ([bar condition true; current bar])
        /// </summary>
        public int MaxPositionOpeningWaitDurationInBars { get; set; }

        /// <summary>
        /// How long to keep opened position if neither stoploss and takeprofit was hit ([bar opened; bar when closing])
        /// </summary>
        public int? MaxOpenPositionDurationInBars { get; set; }

        public bool IsLogIntermediateResults { get; set; }

        public PositionOpeningPredicate PositionOpeningPredicate { get; set; }

        /// <summary>
        /// Called when opening predicate is true
        /// </summary>
        public PositionOpeningDetailsGetter PositionOpeningDetailsGetter { get; set; }

        /// <summary>
        /// Optional. If specified will be used to early close (or bailout in some cases) position based on returned value of the func.
        /// <br/>
        /// model returned - do early close
        /// null returned - do nothing
        /// </summary>
        public PositionEarlyCloseFunc PositionEarlyCloseFunc { get; set; }
    }
}
