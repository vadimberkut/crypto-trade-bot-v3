using System;
using System.Threading.Tasks;

namespace CryptoTradeBot.DownloadMarketData
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await DownloadBinanceHistoryData("./market-data-store");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task DownloadBinanceHistoryData(string storeDirectoryPath)
        {

        }
    }
}
