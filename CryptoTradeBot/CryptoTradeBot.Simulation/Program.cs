using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Extensions.DependencyInjection;
using CryptoTradeBot.Infrastructure.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CryptoTradeBot.Simulation
{
    public class Program
    {
        private const string _appName = "CryptoTradeBot.Simulation";

        public static void Main(string[] args)
        {
            var configuration = GetConfiguration();
            var config = configuration.Get<ApplicationSettings>();

            // here we congigure the Serilog. Nothing special all according documentation of Serilog
            Log.Logger = GetSerilogLogger(configuration, config);

            ShowEnvironmentInfo();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                 .UseSerilog()
                 .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                 .ConfigureWebHostDefaults(webBuilder =>
                 {
                     webBuilder
                        // UseConfiguration must be first to work properly
                        .UseConfiguration(GetConfiguration())

                        // set URLs directly, because it doesn't pick up ASPNETCORE_URLS when UseConfiguration is applied earlier
                        // UseUrls must follow UseConfiguration to work properly
                        .UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://+:80")
                        .UseStartup<Startup>();
                 });
        }

        private static IConfiguration GetConfiguration()
        {
            DotNetEnv.Env.Load(".env");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{HostingEnvironmentHelper.Environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            //// validate ApplicationSettings
            //var config = configuration.Get<ApplicationSettings>();
            //try
            //{
            //    Log.Information("Validating configuration...");
            //    CustomValidationHelper.Validate(config);
            //    Log.Information("Configuration is valid.");
            //}
            //catch (ValidationErrorException ex)
            //{
            //    throw new Exception("Configuration validation failed. Check appsettings.json, appsettings.<environment>.json files.", ex);
            //}

            return configuration;
        }

        private static Serilog.ILogger GetSerilogLogger(IConfiguration configuration, ApplicationSettings config)
        {

            var logger = new LoggerConfiguration()
               .Enrich.WithProperty("ApplicationContext", _appName) //define the context in logged data
               .Enrich.WithProperty("ApplicationEnvironment", HostingEnvironmentHelper.Environment) //define the environment
               .Enrich.FromLogContext() //allows to use specific context if nessesary
               .ReadFrom.Configuration(configuration);

            if (HostingEnvironmentHelper.IsDevelopmentLocalhost())
            {
                // write to file for development purposes
                logger.WriteTo.File(
                    path: Path.Combine("./serilog-logs/local-logs.txt"),
                    fileSizeLimitBytes: 100 * 1024 * 1024, // 100mb
                    restrictedToMinimumLevel: LogEventLevel.Warning
                );
            }

            return logger.CreateLogger();
        }

        private static void ShowEnvironmentInfo()
        {
            Log.Information(Environment.NewLine);
            Log.Information("Environment info:");
            Log.Information("ASPNETCORE_ENVIRONMENT: {EnvironmentVariable}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            Log.Information("ASPNETCORE_URLS: {EnvironmentVariable}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
            Log.Information(Environment.NewLine);
        }
    }
}
