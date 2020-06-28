using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class BarChartIntervalConfig
    {
        /// <summary>
        /// E.g. 4h, 1h
        /// </summary>
        public string Name { get; set; }
        public TimeSpan TimeSpan { get; set; }
    }
}
