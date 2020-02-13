using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Dtos
{
    /*
     {
      "lastUpdateId": 160,  // Last update ID
      "bids": [             // Bids to be updated
        [
          "0.0024",         // Price level to be updated
          "10"              // Quantity
        ]
      ],
      "asks": [             // Asks to be updated
        [
          "0.0026",         // Price level to be updated
          "100"            // Quantity
        ]
      ]
    }
    */
    public class WssPartialBookDepthDto
    {
        public string LastUpdateId { get; set; }

        [JsonProperty("bids")]
        public List<List<string>> SourceBids { get; set; }

        [JsonProperty("asks")]
        public List<List<string>> SourceAsks { get; set; }

        private List<WssPartialBookDepthBidAskDto> _bids = null;
        public List<WssPartialBookDepthBidAskDto> Bids 
        {
            get
            {
                _bids = _bids ?? this.SourceBids.Select(x => new WssPartialBookDepthBidAskDto()
                {
                    Price = decimal.Parse(x[0]),
                    Quantity = decimal.Parse(x[1])
                }).ToList();
                
                return _bids;
            }
        }

        private List<WssPartialBookDepthBidAskDto> _asks = null;
        public List<WssPartialBookDepthBidAskDto> Asks
        {
            get
            {
                _asks = _asks ?? this.SourceAsks.Select(x => new WssPartialBookDepthBidAskDto()
                {
                    Price = decimal.Parse(x[0]),
                    Quantity = decimal.Parse(x[1])
                }).ToList();

                return _asks;
            }
        }
    }

    public class WssPartialBookDepthBidAskDto
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
}
