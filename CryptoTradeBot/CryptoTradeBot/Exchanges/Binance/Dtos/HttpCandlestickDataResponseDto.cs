using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Exchanges.Binance.Dtos
{
    public class HttpCandlestickDataResponseDto
    {
        public HttpCandlestickDataResponseDto()
        {
            Candles = new List<HttpCandlestickDataItemResponseDto>();
        }

        public List<HttpCandlestickDataItemResponseDto> Candles { get; set; }
    }

    public class HttpCandlestickDataItemResponseDto
    {
        public DateTime OpenTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public DateTime CloseTime { get; set; }
        public decimal QuoteAssetVolume { get; set; }
        public int NumberOfTrades { get; set; }
        public decimal TakerBuyBaseAssetVolume { get; set; }
        public decimal TakerBuyQuoteAssetVolume { get; set; }
        public string ApiIgnoredValue { get; set; }
    }
}
