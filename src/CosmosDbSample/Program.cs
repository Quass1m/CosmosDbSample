using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CosmosDbTests
{
    class Program
    {
        private static IConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("COMPUTERNAME", EnvironmentVariableTarget.Process);

            var builder = new ConfigurationBuilder()
                               .SetBasePath(Directory.GetCurrentDirectory())
                               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                               .AddJsonFile($"appsettings.{env}.json", optional: true)
                               .AddEnvironmentVariables()
                               .AddUserSecrets<Program>()
                               .AddCommandLine(args);
            Configuration = builder.Build();

            var settings = Configuration.Get<Settings>();

            // !!! The db and collection must exist !!!
            var resourceId = settings.ResourceId;
            
            var date = DateTime.UtcNow.ToString("R");

            var apiVersion = "2017-02-22";
            var dataFormat = "application/json";
            var method = "POST";

            var token = GenerateAuthToken(
                verb: method,
                resourceType: "docs",
                resourceId: resourceId,
                date: date,
                key: settings.MasterKey,
                keyType: "master",
                tokenVersion: "1.0");

            Console.WriteLine(date);
            Console.WriteLine(token);

            var uri = new Uri($"https://{settings.AccountName}.documents.azure.com:443/{resourceId}/docs");

            using (var client = new HttpClient() { BaseAddress = uri })
            {
                var message = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                };

                message.Headers.Add("x-ms-version", apiVersion);
                message.Headers.Add("Authorization", token);
                message.Headers.Add("x-ms-date", date.ToLowerInvariant());
                message.Headers.Add("Accept", dataFormat);
                message.Content = new StringContent("{\"id\":\"" + Guid.NewGuid() +"\",\"number\":" + DateTime.Now.Ticks + "}", Encoding.UTF8, dataFormat);

                var res = client.SendAsync(message).GetAwaiter().GetResult();
                Console.WriteLine(res.StatusCode);
            }
        }

        // https://docs.microsoft.com/en-gb/rest/api/cosmos-db/access-control-on-cosmosdb-resources?redirectedfrom=MSDN
        static string GenerateAuthToken(string verb, string resourceType, string resourceId, string date, string key, string keyType, string tokenVersion)
        {
            var hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            verb = verb ?? string.Empty;
            resourceType = resourceType ?? string.Empty;
            resourceId = resourceId ?? string.Empty;

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    date.ToLowerInvariant(),
                    string.Empty
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return Uri.EscapeDataString(
                string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "type=" + keyType + "&ver=" + tokenVersion + "&sig=" + signature));
        }
    }
}
