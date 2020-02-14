using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Exchanges.Binance.Dtos
{
    public class HttpExchangeInformationResponseDto
    {
        public HttpExchangeInformationResponseDto()
        {
            Symbols = new List<HttpExchangeInformationSymbolResponseDto>();
        }

        public string Timezone { get; set; }
        public long ServerTime { get; set; }
        //public string rateLimits { get; set; }
        //public string exchangeFilters { get; set; }
        public List<HttpExchangeInformationSymbolResponseDto> Symbols { get; set; }
    }

    public class HttpExchangeInformationSymbolResponseDto
    {
        public HttpExchangeInformationSymbolResponseDto()
        {
            OrderTypes = new List<string>();
        }

        public string Symbol { get; set; }
        public string Status { get; set; }
        public string BaseAsset { get; set; }
        public int BaseAssetPrecision { get; set; }
        public string QuoteAsset { get; set; }
        public int QuotePrecision { get; set; }
        public int BaseCommissionPrecision { get; set; }
        public int QuoteCommissionPrecision { get; set; }
        public List<string> OrderTypes { get; set; }
        public bool IcebergAllowed { get; set; }
        public bool OcoAllowed { get; set; }
        public bool QuoteOrderQtyMarketAllowed { get; set; }
        public bool IsSpotTradingAllowed { get; set; }
        public bool IsMarginTradingAllowed { get; set; }
        //public string filters { get; set; }
    }
}
