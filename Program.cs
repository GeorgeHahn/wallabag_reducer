using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using MoreLinq;
using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    class HNProcessor
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

        public async Task<HNArticle> Extract_HN_article(WallabagClient client, WallabagItem item)
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
    }

    class GoodreadsProcessor
    {
        public async Task Tag_Goodreads_article(WallabagClient client, WallabagItem item)
        {
            // Not a goodreads entry
            if (!item.Url.Contains("goodreads.com"))
                return;
            // Already tagged
            if (item.Tags.Any(t => t.Label == "goodreads"))
                return;
            Console.Write(item.Title);
            await client.AddTagsAsync(item, new[] { "goodreads" });
            Console.WriteLine(" ✓");
        }
    }

    class Program
    {
        static HNProcessor hnx = new HNProcessor();
        static GoodreadsProcessor grx = new GoodreadsProcessor();

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

        static async Task runOne(WallabagContext db, WallabagClient client, WallabagItem item)
        {
            // Filter out HN articles that have already been processed
            var exists = db.ProcessedHNArticles.Any(a => a.SourceWBId == item.Id || a.ContentWBId == item.Id);
            if (!exists)
            {
                var hn = await hnx.Extract_HN_article(client, item);
                if (hn != null)
                {
                    db.ProcessedHNArticles.Add(hn);
                    var count = await db.SaveChangesAsync();
                    Console.WriteLine("{0} HN records saved to database", count);
                }
            }

            await grx.Tag_Goodreads_article(client, item);
        }

        static async Task run(WallabagClient client, int poll_duration)
        {
            // Run all existing items
            var list = await GetAllEntries(client);
            list.ForEach(async item => {
                using (var db = new WallabagContext())
                {
                    await runOne(db, client, item);
                }
            });

            // Get max Wallabag ID from list
            var maxWBId = list.MaxBy(i => i.Id).First().Id + 1;
            Console.WriteLine($"Next Wallabag ID: {maxWBId}");

            // Poll for new items & run them
            while (true)
            {
                await Task.Delay(poll_duration);
                var item = await client.GetItemAsync(maxWBId);
                if (item == null)
                    continue;
                using (var db = new WallabagContext())
                {
                    await runOne(db, client, item);
                }
                maxWBId++;
                Console.WriteLine($"Next Wallabag ID: {maxWBId}");
            }
        }

        static void Main(string[] args)
        {
            var wallabag_url = Environment.GetEnvironmentVariable("WALLABAG_URL");
            var wallabag_client_id = Environment.GetEnvironmentVariable("WALLABAG_CLIENT_ID");
            var wallabag_client_secret = Environment.GetEnvironmentVariable("WALLABAG_CLIENT_SECRET");
            var wallabag_username = Environment.GetEnvironmentVariable("WALLABAG_USERNAME");
            var wallabag_password = Environment.GetEnvironmentVariable("WALLABAG_PASSWORD");
            var wallabag_poll_duration = int.Parse(Environment.GetEnvironmentVariable("WALLABAG_POLL_DURATION_SECONDS")) * 1000;

            WallabagClient client = new WallabagClient(new System.Uri(wallabag_url), wallabag_client_id, wallabag_client_secret);
            var tok = client.RequestTokenAsync(wallabag_username, wallabag_password);
            tok.Wait();

            var ex = run(client, wallabag_poll_duration);
            ex.Wait();
        }
    }
}
