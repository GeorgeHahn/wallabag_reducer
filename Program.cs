using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using MoreLinq;
using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    interface IProcessor
    {
        bool should_run { get; }

        Task Process(WallabagClient client, WallabagItem item);
    }

    class Program
    {
        static async Task<List<WallabagItem>> GetAllEntries(WallabagClient client)
        {
            List<WallabagItem> list = new List<WallabagItem>();
            var page = 1;
            Console.Write("Fetching all entries ");
            while (true)
            {
                Console.Write(".");
                var items = await client.GetItemsAsync(itemsPerPage: 100, pageNumber: page++);
                if (items == null)
                {
                    break;
                }
                list.AddRange(items);
            }
            Console.WriteLine();
            return list;
        }

        static async Task runOne(WallabagClient client, IEnumerable<IProcessor> processors, WallabagItem item)
        {
            foreach (var processor in processors)
            {
                await processor.Process(client, item);
            }
        }

        static async Task run(WallabagClient client, IEnumerable<IProcessor> processors, int poll_duration)
        {
            var valid_processors = processors.Where(proc => proc.should_run);

            // Run all existing items
            var list = await GetAllEntries(client);
            list.ForEach(async item =>
            {
                await runOne(client, valid_processors, item);
            });

            // Get max Wallabag ID from list
            var maxWBId = list.MaxBy(i => i.Id).First().Id + 1;
            Console.WriteLine($"Next Wallabag ID: {maxWBId}");

            // Poll for new items & run them
            while (true)
            {
                var item = await client.GetItemAsync(maxWBId);
                if (item == null)
                {
                    await Task.Delay(poll_duration);
                    continue;
                }
                await runOne(client, valid_processors, item);
                maxWBId++;
                Console.WriteLine($"Next Wallabag ID: {maxWBId}");
            }
        }

        class AppConfig
        {
            public string url { get; set; }
            public string client_id { get; set; }
            public string client_secret { get; set; }
            public string username { get; set; }
            public string password { get; set; }
            public int poll_duration { get; set; }
            public string data_dir { get; set; }
            public string database_file { get; set; }
            public string config_file { get; set; }

            public bool Validate()
            {
                if(string.IsNullOrWhiteSpace(url)) {
                    throw new Exception("WALLABAG_URL not set");
                }
                if(string.IsNullOrWhiteSpace(client_id)) {
                    throw new Exception("WALLABAG_CLIENT_ID not set");
                }
                if(string.IsNullOrWhiteSpace(client_secret)) {
                    throw new Exception("WALLABAG_CLIENT_SECRET not set");
                }
                if(string.IsNullOrWhiteSpace(username)) {
                    throw new Exception("WALLABAG_USERNAME not set");
                }
                if(string.IsNullOrWhiteSpace(password)) {
                    throw new Exception("WALLABAG_PASSWORD not set");
                }
                return true;
            }
        }

        static int Main(string[] args)
        {
            var appConfig = new AppConfig
            {
                url = Environment.GetEnvironmentVariable("WALLABAG_URL"),
                client_id = Environment.GetEnvironmentVariable("WALLABAG_CLIENT_ID"),
                client_secret = Environment.GetEnvironmentVariable("WALLABAG_CLIENT_SECRET"),
                username = Environment.GetEnvironmentVariable("WALLABAG_USERNAME"),
                password = Environment.GetEnvironmentVariable("WALLABAG_PASSWORD"),
                poll_duration = int.Parse(Environment.GetEnvironmentVariable("WALLABAG_POLL_DURATION_SECONDS") ?? "60") * 1000,
                data_dir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "",
                database_file = "wallabag_reducer.sqlite3",
                config_file = "config.json"
            };

            WallabagContext.database_file = Path.Combine(appConfig.data_dir, appConfig.database_file);

            var config_path = Path.Combine(appConfig.data_dir, appConfig.config_file);
            if (!File.Exists(config_path))
            {
                Console.WriteLine($"No config file, exiting (expected at {config_path}");
                return -1;
            }

            try
            {
                appConfig.Validate();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid environment configuration: {e.Message}");
                return -1;
            }

            // Load processor config file
            var processorConfig = JObject.Parse(File.ReadAllText(config_path));

            // Create DB & run any necessary migrations
            using (var db = new WallabagContext())
            {
                db.Database.Migrate();
            }

            // Create & auth with Wallabag instance
            WallabagClient client = new WallabagClient(new System.Uri(appConfig.url), appConfig.client_id, appConfig.client_secret);
            var tok = client.RequestTokenAsync(appConfig.username, appConfig.password);
            tok.Wait();

            // Prep processors
            var processors = new IProcessor[] {
                new HNProcessor(),
                new GenericTagProcessor(processorConfig)
            };

            var ex = run(client, processors, appConfig.poll_duration);
            ex.Wait();
            return 0;
        }
    }
}
