using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.Config
{
    public static class SerializationConfig
    {
        /// <summary>
        /// Sets default serialization settings
        /// </summary>
        /// <param name="jsonSerializerSettings"></param>
        /// <returns></returns>
        public static JsonSerializerSettings GetDefaultJsonSerializerSettings(JsonSerializerSettings jsonSerializerSettings)
        {
            jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter(new DefaultNamingStrategy())); // serialize enums as string, not int

            // convert input and output DateTime values in models to UTC 
            //The default converter of DateTime for models is ISO converter
            //We can change it or add custom converter to accept custom formats or convert response DateTime for user's TimeZone. 
            //Docs: https://app.nuclino.com/Unicreo/IoT/Json-Converters-18c6f603-8f01-425b-8ab9-2ad6b4d8ba58
            jsonSerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            return jsonSerializerSettings;
        }

        /// <summary>
        /// Returns default serialization settings
        /// </summary>
        /// <returns></returns>
        public static JsonSerializerSettings GetDefaultJsonSerializerSettings()
        {
            return GetDefaultJsonSerializerSettings(new JsonSerializerSettings());
        }
    }
}
