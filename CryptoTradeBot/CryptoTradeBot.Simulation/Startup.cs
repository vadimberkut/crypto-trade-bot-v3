using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptoTradeBot.Exchanges.Binance;
using CryptoTradeBot.Exchanges.Binance.Handlers;
using CryptoTradeBot.Exchanges.Binance.Stores;
using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.Host.Exchanges.Binance.Utils;
using CryptoTradeBot.Infrastructure.Utils;
using CryptoTradeBot.Simulation.Implementations;
using CryptoTradeBot.Simulation.Workers;
using CryptoTradeBot.StrategyRunner;
using CryptoTradeBot.StrategyRunner.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTradeBot.Simulation
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<ApplicationSettings>(Configuration);

            services.AddHttpClient();
            services.AddHttpContextAccessor();

            // utils
            services.AddTransient<HttpUtil>();

            // add workers
            services.AddHostedService<AlgorithmSimulationWorkerService>();

            // binance
            services.AddTransient<BinanceHttpClient>(sp => {
                return new BinanceHttpClient(
                    sp.GetService<ILogger<BinanceHttpClient>>(),
                    sp.GetService<IOptions<ApplicationSettings>>().Value.Binance,
                    sp.GetService<HttpUtil>()
                );
            });
            services.AddTransient<BinanceWssClient>(sp => {
                return new BinanceWssClient(
                    sp.GetService<ILogger<BinanceWssClient>>(),
                    sp.GetService<IOptions<ApplicationSettings>>().Value.Binance
                );
            });
            services.AddSingleton<BinanceExchangeUtil>();
            services.AddTransient<WssBookDepthHandler>();
            services.AddSingleton<OrderBookStore>();

            // strategy runner and its dependencies
            services.AddTransient<StrategyRunnerService>();
            services.AddTransient<IMarketHistoryDataSource, BinanceMarketHistoryDataSource>();

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
