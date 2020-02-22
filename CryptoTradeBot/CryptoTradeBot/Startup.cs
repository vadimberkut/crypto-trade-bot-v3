using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using CryptoTradeBot.Exchanges.Binance;
using CryptoTradeBot.Exchanges.Binance.Handlers;
using CryptoTradeBot.Exchanges.Binance.Stores;
using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.Host.Exchanges.Binance.Utils;
using CryptoTradeBot.Infrastructure.Utils;
using CryptoTradeBot.WorkerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTradeBot
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
            services.AddHostedService<BotWorkerService>();

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

            services.AddControllers();
        }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you. If you
        // need a reference to the container, you need to use the
        // "Without ConfigureContainer" mechanism (see docs).
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule(new AutofacDefaultModule());
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
