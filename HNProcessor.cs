using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;

using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;
using Xunit;

namespace WallabagReducer.Net
{
    public class HNProcessorTests
    {
        [Fact]
        public async Task StoryLinks_Extract_Correctly()
        {
            var p = new HNProcessor();
            var storylink = await p.Extract_story_link("https://news.ycombinator.com/item?id=19810504");
            Assert.Equal("https://m.stopa.io/risp-lisp-in-rust-90a0dad5b116", storylink);
        }

        [Fact]
        public async Task SelfLinks_Extract_Correctly()
        {
            var p = new HNProcessor();
            var storylink = await p.Extract_story_link("https://news.ycombinator.com/item?id=19822801");
            // Assert.Equal("item?id=19822801", storylink);
            Assert.Null(storylink);
        }
    }

    public class HNProcessor : IProcessor
    {
        Regex _regex;
        Regex regex()
        {
            if (_regex == null)
            {
                _regex = new Regex("<a href=\"([a-z]{2,6}://[-a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b[-a-zA-Z0-9@:%_\\+.~#?&//=]*)\"[^>]*class=\"storylink\">",
                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            return _regex;
        }

        public bool should_run { get; set; } = true;

        private HttpClient fetcher = new HttpClient();

        public async Task<string> Extract_story_link(string uri)
        {
            string responseBody = await fetcher.GetStringAsync(uri);

            var doc = new HtmlDocument();
            doc.LoadHtml(responseBody);
            var node = doc
                .DocumentNode
                .SelectSingleNode("//a[contains(@class, 'storylink')]");

            // Ignore no match posts
            if (node == null)
                return null;

            var value = node.Attributes["href"].Value;

            // Ignore self posts (link back to the same story)
            if (uri.Contains(value))
                return null;

            return value;
        }

        public async Task<HNArticle> Extract_HN_article(WallabagClient client, WallabagItem item)
        {
            // Not a HN article
            if (!item.Url.Contains("news.ycombinator"))
                return null;

            try
            {
                string content_url = await Extract_story_link(item.Url);

                // Ignore no match posts
                if (content_url == null)
                    return null;

                Console.Write($"Adding {content_url}");
                WallabagItem newItem = await client.AddAsync(
                    new Uri(content_url),
                    tags: new string[] { "hacker-news" }
                );
                Console.WriteLine(" ✓");
                if (newItem.Url != content_url)
                {
                    Console.WriteLine($"URLs don't match: {content_url} {newItem.Url}");
                }

                return new HNArticle
                {
                    SourceWBId = item.Id,
                    SourceUrl = item.Url,
                    ContentWBId = newItem.Id,
                    ContentUrl = content_url
                };
            }
            catch(HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ",e.Message);
            }
            return null;
        }

        public async Task Process(WallabagClient client, WallabagItem item)
        {
            using (var db = new WallabagContext())
            {
                // Filter out HN articles that have already been processed
                var exists = db.ProcessedHNArticles.Any(a => a.SourceWBId == item.Id || a.ContentWBId == item.Id);
                if (!exists)
                {
                    var hn = await Extract_HN_article(client, item);
                    if (hn != null)
                    {
                        db.ProcessedHNArticles.Add(hn);
                        var count = await db.SaveChangesAsync();
                        Console.WriteLine("{0} HN records saved to database", count);
                    }
                }
            }
        }
    }
}
