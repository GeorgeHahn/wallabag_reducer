using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using wallabag.Api;
using wallabag.Api.Models;

namespace WallabagReducer.Net
{
    class GenericTagProcessor : IProcessor
    {
        private readonly IList<Mapping> mappings;

        public bool should_run { get; set; } = true;

        class Mapping
        {
            public string domain { get; set; }
            public string tag { get; set; }
        }

        public GenericTagProcessor(JObject config)
        {
            mappings = config["GenericTagProcessor"]?["tag_mapping"]?.ToObject<IList<Mapping>>();
            should_run = mappings != null;
        }

        public async Task Process(WallabagClient client, WallabagItem item)
        {
            foreach (var mapping in mappings)
            {
                // Not a matching entry
                if (!item.Url.Contains(mapping.domain))
                    continue;
                // Already tagged
                if (item.Tags.Any(t => t.Label == mapping.tag))
                    continue;
                Console.Write(item.Title.Replace("\n", " "));
                await client.AddTagsAsync(item, new[] { mapping.tag });
                Console.WriteLine($" {mapping.tag} ✓");
            }
        }
    }
}
