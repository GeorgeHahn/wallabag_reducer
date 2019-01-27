using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WallabagReducer.Net
{
    public class WallabagContext : DbContext
    {
        public DbSet<HNArticle> ProcessedHNArticles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=wallabag_reducer.sqlite3");
        }
    }

    public class HNArticle
    {
        public Guid Id { get; set; }
        public int SourceWBId { get; set; }
        public string SourceUrl { get; set; }
        public int ContentWBId { get; set; }
        public string ContentUrl { get; set; }
    }
}