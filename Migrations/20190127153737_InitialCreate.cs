using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WallabagReducer.Net.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedHNArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    SourceWBId = table.Column<int>(nullable: false),
                    SourceUrl = table.Column<string>(nullable: true),
                    ContentWBId = table.Column<int>(nullable: false),
                    ContentUrl = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedHNArticles", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedHNArticles");
        }
    }
}
