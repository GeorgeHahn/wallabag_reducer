using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    /// Send pages on given domains to youtube-dl-server
    /// See: https://github.com/manbearwiz/youtube-dl-server
    class YoutubeDownloader : IProcessor
    {
        private readonly Config config;

        public bool should_run { get; set; } = true;

        private HttpClient fetcher = new HttpClient();

        private Regex ytregex = new Regex("www.youtube\\.com\\/oembed\\?.*url=(.*)");

        class Config
        {
            public List<String> url_filter { get; set; }
            public bool mark_read { get; set; }
            public string tag_name { get; set; }
            public string youtube_dl_server { get; set; }
        }

        public YoutubeDownloader(JObject config)
        {
            this.config = config["YoutubeDownloader"].ToObject<Config>();
        }

        public async Task Process(WallabagClient client, WallabagItem item)
        {
            bool filter_match = false;
            foreach (var filter in config.url_filter)
            {
                if (item.Url.Contains(filter)) {
                    filter_match = true;
                    break;
                }
            }

            // Not from a matching domain
            if(!filter_match) {
                return;
            }

            // Already tagged
            if (item.Tags.Any(t => t.Label == config.tag_name)) {
                return;
            }

            Console.Write(item.Title.Replace("\n", " "));

            // Extract youtube url from the oembed url that wallabag gives
            var url = item.Url;
            var match = ytregex.Match(url);
            if (match.Success) {
                url = match.Captures[0].Value;
                Console.WriteLine("Transformed youtube oembed url");
            }

            // Send DL request to Youtube-DL-Server
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("url", url),
                new KeyValuePair<string, string>("format", "bestvideo"),
            });
            var dl_request = await fetcher.PostAsync(config.youtube_dl_server, content);
            var response = await dl_request.Content.ReadAsStringAsync();
            Console.WriteLine(response);

            await client.AddTagsAsync(item, new[] { config.tag_name });

            if (config.mark_read) {
                await client.ArchiveAsync(item);
            }

            Console.WriteLine($" ✓");
        }
    }
}
