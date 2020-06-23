using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Exchanges.Binance
{
    public static class BinanceConfig
    {
        public const string USDTAsset = "USDT";

        /// <summary>
        /// High volume symbols (usually)
        /// </summary>
        public static List<string> HighVolumeSymbols = new List<string>()
        {
            // BTC
            $"ETHBTC",
            $"XRPBTC",
            $"HBARBTC",
            $"BNBBTC",
            $"LTCBTC",
            $"CHZBTC",
            $"WRXBTC",
            $"ETCBTC",
            $"XTZBTC",
            $"TRXBTC",
            $"EOSBTC",
            $"ADABTC",
            $"NEOBTC",
            $"BCHBTC",
            $"XLMBTC",
            $"XMRBTC",
            $"IOTABTC",
            $"DASHBTC",

            // USDT
            $"BTCUSDT",
            $"ETHUSDT",
            $"XRPUSDT",
            $"BNBUSDT",
            $"LTCUSDT",
            $"BCHUSDT",
            $"ETCUSDT",
            $"EOSUSDT",
            $"TRXUSDT",
            $"HBARUSDT",
            $"ADAUSDT",
            $"XLMUSDT",
            $"XTZUSDT",
            $"NEOUSDT",
            $"DASHUSDT",
            $"IOTAUSDT",
            $"XMRUSDT",

            // BNB
            $"FTTBNB",
            $"XRPBNB",
            $"HBARBNB",
            $"TRXBNB",
            $"XTZBNB",
            $"CHZBNB",
            $"LTCBNB",
            $"EOSBNB",
            $"ADABNB",
            $"BCHBNB",
            $"ETCBNB",
            $"IOTABNB",
            $"XLMBNB",
            $"NEOBNB",
            $"DASHBNB",
            $"XMRBNB",
            $"LSKBNB",

            // BUSD
            $"BTCBUSD",
            $"XRPBUSD",
            $"ETHBUSD",
            $"BNBBUSD",
            $"LTCBUSD",
            $"TRXBUSD",
            $"XLMBUSD",
            $"BCHBUSD",
            $"EOSBUSD",
            $"ADABUSD",
            $"XTZBUSD",
            $"ETCBUSD",
            $"NEOBUSD",
            $"DASHBUSD",

            // TUSD
            $"BTCTUSD",
            $"ETHTUSD",
            $"BCHTUSD",
            $"XRPTUSD",
            $"LTCTUSD",
            $"XLMTUSD",
            $"TRXTUSD",
            $"NEOTUSD",
            $"ADATUSD",
            $"EOSTUSD",
            $"BNBTUSD",
            $"USDCTUSD",

            // USDC
            $"BTCUSDC",
            $"ETHUSDC",
            $"XRPUSDC",
            $"LTCUSDC",
            $"BCHUSDC",
            $"TRXUSDC",
            $"BNBUSDC",
            $"EOSUSDC",
            $"NEOUSDC",
            $"ADAUSDC",

            // ETH
            $"BNBETH",
            $"XRPETH",
            $"EOSETH",
            $"TRXETH",
            $"ADAETH",
            $"LTCETH",
            $"ETCETH",
            $"XLMETH",
            $"NEOETH",
            $"IOTAETH",
            $"XMRETH",
            $"DASHETH",

            // TRX

            // XRP
                $"TRXXRP",
        };

        /// <summary>
        /// https://github.com/binance-exchange/binance-official-api-docs/blob/master/rest-api.md
        /// </summary>
        public static List<BinanceCandlestickChartIntervalConfig> CandlestickChartIntervals = new List<BinanceCandlestickChartIntervalConfig>
        {
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "1m",
                TimeSpan = TimeSpan.FromMinutes(1),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "3m",
                TimeSpan = TimeSpan.FromMinutes(3),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "5m",
                TimeSpan = TimeSpan.FromMinutes(5),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "15m",
                TimeSpan = TimeSpan.FromMinutes(15),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "30m",
                TimeSpan = TimeSpan.FromMinutes(30),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "1h",
                TimeSpan = TimeSpan.FromHours(1),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "2h",
                TimeSpan = TimeSpan.FromHours(2),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "4h",
                TimeSpan = TimeSpan.FromHours(4),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "6h",
                TimeSpan = TimeSpan.FromHours(6),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "8h",
                TimeSpan = TimeSpan.FromHours(8),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "12h",
                TimeSpan = TimeSpan.FromHours(12),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "1d",
                TimeSpan = TimeSpan.FromDays(1),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "3d",
                TimeSpan = TimeSpan.FromDays(3),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "1w",
                TimeSpan = TimeSpan.FromDays(7),
            },
            new BinanceCandlestickChartIntervalConfig()
            {
                Name = "1M",
                TimeSpan = TimeSpan.FromDays(30), // roughly assume 30 days
            },
        };

        public static BinanceCandlestickChartIntervalConfig GetCandlestickChartInterval(string name)
        {
            var config = CandlestickChartIntervals.FirstOrDefault(x => x.Name == name);
            if(config == null)
            {
                throw new ArgumentException();
            }
            return config;
        }
    }

    public class BinanceCandlestickChartIntervalConfig
    {
        public string Name { get; set; }
        public TimeSpan TimeSpan { get; set; }
    }

}
