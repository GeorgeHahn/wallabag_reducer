using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    class HNProcessor : IProcessor
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

        public bool should_run { get; set; } = false;

        async Task<HNArticle> Extract_HN_article(WallabagClient client, WallabagItem item)
        {
            // Not a HN article
            if (!item.Url.Contains("news.ycombinator"))
                return null;
            // Not scraped correctly?
            if (item.Content == null)
                return null;

            // Search for HN storylink
            var matches = regex().Matches(item.Content);
            // Ignore no match posts (Ask HN, etc)
            if (matches.Count == 0 || matches[0].Groups.Count <= 1)
                return null;
            var content_url = matches[0].Groups[1].Value;
            // Ignore self HN posts
            if (content_url.Contains("news.ycombinator"))
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
