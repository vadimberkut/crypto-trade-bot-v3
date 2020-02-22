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
    }
}
