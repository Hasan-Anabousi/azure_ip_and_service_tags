using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace azure_ip_and_service_tags
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string directory = "ips_data";
            string url = "https://download.microsoft.com/download/7/1/D/71D86715-5596-4529-9B13-DA13A5DE5B63/ServiceTags_Public_20240826.json";
            string oldFileName = Path.Combine(directory, "old_ips.json");
            string newFileName = Path.Combine(directory, "new_ips.json");
            string changeLogFileName = Path.Combine(directory, "change_log_" + DateTime.UtcNow.ToString("yyyyMMdd") + ".txt");

            Directory.CreateDirectory(directory);  // Ensure the directory exists

            try
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(url);
                string newJson = await response.Content.ReadAsStringAsync();
                File.WriteAllText(newFileName, newJson);

                string oldJson;
                if (!File.Exists(oldFileName))
                {
                    oldJson = "{}";
                    File.WriteAllText(oldFileName, oldJson);
                    Console.WriteLine("No previous data found. Starting new data tracking.");
                }
                else
                {
                    oldJson = File.ReadAllText(oldFileName);
                }

                JObject oldData = JObject.Parse(oldJson);
                JObject newData = JObject.Parse(newJson);

                // Extract and compare only if IDs exist in both files
                var comparisonResult = CompareServiceTags(oldData, newData);

                using (StreamWriter file = new StreamWriter(changeLogFileName))
                {
                    if (comparisonResult.AddedIPs.Count > 0)
                    {
                        file.WriteLine("Added IPs:");
                        foreach (var ip in comparisonResult.AddedIPs)
                        {
                            file.WriteLine(ip);
                        }
                    }

                    if (comparisonResult.RemovedIPs.Count > 0)
                    {
                        file.WriteLine("Removed IPs:");
                        foreach (var ip in comparisonResult.RemovedIPs)
                        {
                            file.WriteLine(ip);
                        }
                    }

                    if (comparisonResult.AddedIPs.Count == 0 && comparisonResult.RemovedIPs.Count == 0)
                    {
                        file.WriteLine("No changes detected.");
                    }
                }

                // Update old file with new data for future comparison
                File.WriteAllText(oldFileName, newJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
            Console.ReadLine();
        }

        static (HashSet<string> AddedIPs, HashSet<string> RemovedIPs) CompareServiceTags(JObject oldData, JObject newData)
        {
            var oldTags = ExtractIPAddresses(oldData);
            var newTags = ExtractIPAddresses(newData);

            var commonIds = oldTags.Keys.Intersect(newTags.Keys).ToList();
            HashSet<string> addedIPs = new HashSet<string>();
            HashSet<string> removedIPs = new HashSet<string>();

            foreach (var id in commonIds)
            {
                var oldIPs = oldTags[id];
                var newIPs = newTags[id];

                foreach (var ip in newIPs)
                {
                    if (!oldIPs.Contains(ip))
                        addedIPs.Add($"{id}: {ip}");
                }

                foreach (var ip in oldIPs)
                {
                    if (!newIPs.Contains(ip))
                        removedIPs.Add($"{id}: {ip}");
                }
            }

            return (addedIPs, removedIPs);
        }

        static Dictionary<string, HashSet<string>> ExtractIPAddresses(JObject data)
        {
            var tags = new Dictionary<string, HashSet<string>>();
            foreach (var value in data["values"] ?? new JArray())
            {
                var id = value["id"]?.ToString();
                var ips = new HashSet<string>();
                var prefixes = value["properties"]?["addressPrefixes"];
                if (prefixes is JArray array)
                {
                    foreach (var item in array)
                    {
                        ips.Add(item.ToString());
                    }
                }
                if (id != null)
                {
                    tags[id] = ips;
                }
            }
            return tags;
        }
    }
}

//
//string clientId = "your-client-id";
//string tenantId = "your-tenant-id";
//string clientSecret = "your-client-secret";
//string subscriptionId = "your-subscription-id";

//var credentials = new ClientSecretCredential(tenantId, clientId, clientSecret);
//var armClient = new ArmClient(credentials, subscriptionId);

//var subscription = await armClient.GetDefaultSubscriptionAsync();
//var serviceTags = await subscription.GetTagsAsync();

//foreach (var tag in serviceTags.Value)
//{
//    Console.WriteLine($"Tag Name: {tag.TagName}");
//    foreach (var value in tag.Values)
//    {
//        Console.WriteLine($"Service: {value.Id}, Address Prefixes: {string.Join(", ", value.Properties.AddressPrefixes)}");
//    }
//}
