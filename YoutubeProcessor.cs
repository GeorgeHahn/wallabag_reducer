﻿using System;
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
    public class YoutubeProcessorTests
    {
        [Fact]
        public void Youtube_Oembed_Transforms_Correctly()
        {
            var p = new YoutubeDownloader();
            var storylink = p.Extract_yt_oembed("https://www.youtube.com/oembed?format=xml&url=https://www.youtube.com/watch?v=_uFyp1WS1Fw&list=FL_vCZnb8HaQ-X02g6dSzBlQ");
            Assert.Equal("https://www.youtube.com/watch?v=_uFyp1WS1Fw&list=FL_vCZnb8HaQ-X02g6dSzBlQ", storylink);
        }

        [Fact]
        public void Non_Oembed_Isnt_Transformed()
        {
            var p = new YoutubeDownloader();
            var storylink = p.Extract_yt_oembed("https://www.youtube.com/watch?v=_uFyp1WS1Fw&list=FL_vCZnb8HaQ-X02g6dSzBlQ");
            Assert.Equal("https://www.youtube.com/watch?v=_uFyp1WS1Fw&list=FL_vCZnb8HaQ-X02g6dSzBlQ", storylink);
        }
    }

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
        public string Extract_yt_oembed(string url)
        {
            var match = ytregex.Match(url);
            if (match.Success && match.Groups.Count == 2) {
                return match.Groups[1].Value;
            } else {
                return url;
            }
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

            var url = this.Extract_yt_oembed(item.Url);

            // Send DL request to Youtube-DL-Server
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("url", url),
                new KeyValuePair<string, string>("format", "(\"bestvideo[width>=1920]\"/bestvideo)+bestaudio/best"),
            });
            var dl_request = await fetcher.PostAsync(config.youtube_dl_server, content);
            if (dl_request.StatusCode != System.Net.HttpStatusCode.OK) {
                Console.WriteLine($" ✗ yt-dl-server returned error response");
                return;
            }
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
