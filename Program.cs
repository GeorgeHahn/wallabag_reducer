using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    class Program
    {
        static async Task<List<WallabagItem>> GetAllEntries(WallabagClient client)
        {
            List<WallabagItem> list = new List<WallabagItem>();
            var page = 1;
            while(true) {
                Console.Write(".");
                var items = await client.GetItemsAsync(itemsPerPage: 100, pageNumber: page++);
                if(items == null) {
                    break;
                }
                list.AddRange(items);
            }
            Console.WriteLine();
            return list;
        }

        static async Task<IEnumerable<HNArticle>> Extract_HN_articles(WallabagClient client, IEnumerable<WallabagItem> list)
        {
            var results = new List<HNArticle>();
            Regex rx = new Regex("<a href=\"([a-z]{2,6}://[-a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b[-a-zA-Z0-9@:%_\\+.~#?&//=]*)\"[^>]*class=\"storylink\">",
                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach(var item in list) {
                if(!item.Url.Contains("news.ycombinator"))
                    continue;
                if(item.Content == null)
                    continue;

                // Search for HN storylink
                var matches = rx.Matches(item.Content);
                // Ignore no match posts (Ask HN, etc)
                if(matches.Count == 0 || matches[0].Groups.Count <= 1)
                    continue;
                var content_url = matches[0].Groups[1].Value;
                // Ignore self HN posts
                if(content_url.Contains("news.ycombinator"))
                    continue;

                Console.Write($"Adding {content_url}");
                WallabagItem newItem = await client.AddAsync(
                    new Uri(content_url),
                    tags: new string[] { "hacker-news" }
                );
                Console.WriteLine(" ✓");
                if(newItem.Url != content_url) {
                    Console.WriteLine($"URLs don't match: {content_url} {newItem.Url}");
                }

                results.Add(new HNArticle {
                    SourceWBId = item.Id,
                    SourceUrl = item.Url,
                    ContentWBId = newItem.Id,
                    ContentUrl = content_url
                    });
            }
            return results;
        }

        static async Task Tag_Goodreads_articles(WallabagClient client, List<WallabagItem> list)
        {
            foreach(var item in list) {
                if(!item.Url.Contains("goodreads.com"))
                    continue;
                if(item.Tags.Any(t => t.Label == "goodreads"))
                    continue;
                Console.Write(item.Title);
                await client.AddTagsAsync(item, new [] { "goodreads" } );
                Console.WriteLine(" ✓");
            }
        }

        static async Task run(WallabagClient client)
        {
            var list = await GetAllEntries(client);
            using (var db = new WallabagContext())
            {
                // Filter out HN articles that have already been processed
                var hnprocessed = db.ProcessedHNArticles.ToList();
                var hnlist = from item in list
                             where hnprocessed.FindAll(h =>
                                    h.SourceWBId == item.Id ||
                                    h.ContentWBId == item.Id)
                                .Count == 0
                             select item;
                IEnumerable<HNArticle> hn = null;
                try
                {
                    hn = await Extract_HN_articles(client, hnlist);
                }
                catch
                {
                    throw;
                }
                db.ProcessedHNArticles.AddRange(hn);
                var count = db.SaveChanges();
                Console.WriteLine("{0} HN records saved to database", count);
            }

            await Tag_Goodreads_articles(client, list);
        }

        static void Main(string[] args)
        {
            WallabagClient client = new WallabagClient(new System.Uri("url"), "clientid", "secret");
            var tok = client.RequestTokenAsync("username", "password");
            tok.Wait();

            var ex = run(client);
            ex.Wait();
        }
    }
}
