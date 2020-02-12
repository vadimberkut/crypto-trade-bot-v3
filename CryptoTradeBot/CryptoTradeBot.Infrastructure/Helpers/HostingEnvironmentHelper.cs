using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoTradeBot.Infrastructure.Helpers
{
    public static class HostingEnvironmentHelper
    {
        public static class HostingEnvironmentDefaults
        {
            // default names
            public const string Development = "Development";
            public const string Staging = "Staging";
            public const string Production = "Production";

            // custom names
            public const string DevelopmentLocalhost = "DevelopmentLocalhost";
            public const string DevelopmentDocker = "DevelopmentDocker";
            public const string DevelopmentAzure = "DevelopmentAzure";

            // TODO
            public const string TestingLocalhost = "TestingLocalhost";
            public const string TestingAzureDevOps = "TestingAzureDevOps";

            public const string ProductionAzure = "ProductionAzure";
        }

        public static string Environment
        {
            get
            {
                // for web projects
                string env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

                // for console projects
                if (String.IsNullOrEmpty(env))
                {
                    env = System.Environment.GetEnvironmentVariable("Environment");
                }

                if (String.IsNullOrEmpty(env))
                {
                    // if we get here then Environment is possibly set in hostsettings.json and can't be accessed
                    // only using 'hostingContext.HostingEnvironment.EnvironmentName', so it is not in
                    // 'Environment' env variable
                    throw new Exception("Neither ASPNETCORE_ENVIRONMENT nor Environment is set! Recheck startup configuration.");
                }

                return env;
            }
        }

        public static bool IsDevelopmentLocalhost(string environment)
        {
            return environment == HostingEnvironmentDefaults.DevelopmentLocalhost;
        }

        public static bool IsDevelopmentLocalhost()
        {
            return Environment == HostingEnvironmentDefaults.DevelopmentLocalhost;
        }

        public static bool IsDevelopmentDocker(string environment)
        {
            return environment == HostingEnvironmentDefaults.DevelopmentDocker;
        }

        public static bool IsDevelopmentDocker()
        {
            return Environment == HostingEnvironmentDefaults.DevelopmentDocker;
        }

        public static bool IsDevelopmentAzure(string environment)
        {
            return environment == HostingEnvironmentDefaults.DevelopmentAzure;
        }

        public static bool IsDevelopmentAzure()
        {
            return Environment == HostingEnvironmentDefaults.DevelopmentAzure;
        }

        /// <summary>
        /// Returns true if current environment is Development or any custom type like DevelopmentLocalhost, DevelopmentDocker, Development[anything]
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        public static bool IsDevelopmentAny(string environment)
        {
            var regex = new Regex(@"^Development[a-zA-Z0-9_-]{0,}$");
            return regex.IsMatch(environment);
        }

        public static bool IsDevelopmentAny()
        {
            return IsDevelopmentAny(Environment);
        }

        public static bool IsTestingAny(string environment)
        {
            var regex = new Regex(@"^Testing[a-zA-Z0-9_-]{0,}$");
            return regex.IsMatch(environment);
        }

        public static bool IsTestingAny()
        {
            return IsTestingAny(Environment);
        }

        public static bool IsProductionAzure(string environment)
        {
            return environment == HostingEnvironmentDefaults.ProductionAzure;
        }

        public static bool IsProductionAzure()
        {
            return Environment == HostingEnvironmentDefaults.ProductionAzure;
        }

        public static bool IsProductionAny(string environment)
        {
            var regex = new Regex(@"^Production[a-zA-Z0-9_-]{0,}$");
            return regex.IsMatch(environment);
        }

        public static bool IsProductionAny()
        {
            return IsProductionAny(Environment);
        }
    }
}
