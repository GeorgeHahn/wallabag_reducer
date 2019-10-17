using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;
using Xunit;

namespace WallabagReducer.Net
{
    /// Send pages on given domains to youtube-dl-server
    /// See: https://github.com/manbearwiz/youtube-dl-server
    class YoutubeDownloader : IProcessor
    {
        private readonly Config config;

        public bool should_run { get; set; } = true;

        private HttpClient fetcher = new HttpClient();

        private Regex lastditchregex = new Regex("(https:\\/\\/www\\.youtube\\.com\\/watch\\?v=[\\-_a-zA-Z0-9]*)");

        private string[] blacklist = new [] {
            // ytdl can't download these
            "youtube.com/oembed",

            // ytdl-server will try to download the whole list
            // may get last-ditch extracted
            "list=",
        };

        class Config
        {
            public List<String> url_filter { get; set; }
            public bool mark_read { get; set; }
            public string tag_name { get; set; }
            public string youtube_dl_server { get; set; }
            public string format { get; set; }
        }

        public YoutubeDownloader()
        {
            this.config = new Config();
        }

        public YoutubeDownloader(JObject config)
        {
            this.config = config["YoutubeDownloader"].ToObject<Config>();
        }

        // Extract youtube url from the oembed url that wallabag gives
        // Workaround for https://github.com/wallabag/wallabag/issues/3638
        private string extract_yt_lastditch(string url)
        {
            var match = this.lastditchregex.Match(url);
            if (match.Success && match.Groups.Count == 2) {
                return match.Groups[1].Value;
            } else {
                return null;
            }
        }

        public async Task Process(WallabagClient client, WallabagItem item)
        {
            bool filter_match = false;
            String url = null;
            foreach (var filter in config.url_filter)
            {
                // Prefer matches on OriginalUrl
                if (!String.IsNullOrWhiteSpace(item.OriginalUrl) && item.OriginalUrl.Contains(filter)) {
                    filter_match = true;
                    url = item.OriginalUrl;

                    break;
                }

                if (item.Url.Contains(filter)) {
                    filter_match = true;
                    url = item.Url;

                    break;
                }
            }

            // Not from a matching domain
            if(!filter_match) {
                return;
            }

            foreach (var bl in blacklist) {
                if(url.Contains(bl)) {
                    var oldurl = url;
                    url = extract_yt_lastditch(url);

                    if(url == null) {
                        Console.WriteLine($"Warning: YoutubeProcessor detected blacklisted pattern; skipping {oldurl}");
                    } else {
                        Console.WriteLine($"Info: YoutubeProcessor detected blacklisted pattern; extracted {url} from {oldurl}");
                    }

                    return;
                }
            }

            // Already tagged
            if (item.Tags.Any(t => t.Label == config.tag_name)) {
                return;
            }

            Console.Write(item.Title.Replace("\n", " "));

            // Send DL request to Youtube-DL-Server
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("url", url),
                new KeyValuePair<string, string>("format", config.format),
            });

            try {
                var dl_request = await fetcher.PostAsync(config.youtube_dl_server, content);
                if (dl_request.StatusCode != System.Net.HttpStatusCode.OK) {
                    Console.WriteLine($" ✗ yt-dl-server returned error response");
                    return;
                }
                var response = await dl_request.Content.ReadAsStringAsync();

                await client.AddTagsAsync(item, new[] { config.tag_name });

                if (config.mark_read) {
                    await client.ArchiveAsync(item);
                }

                Console.WriteLine($" ✓");
            }
            catch (Exception e) {
                Console.WriteLine($"Error submitting {url}\n{e.ToString()}\n{e.StackTrace.ToString()}");
            }
        }
    }
}
