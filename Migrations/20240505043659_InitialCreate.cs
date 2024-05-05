using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WSTKNG.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SMTPHost = table.Column<string>(type: "TEXT", nullable: false),
                    SMTPPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SMTPUser = table.Column<string>(type: "TEXT", nullable: false),
                    SMTPPassword = table.Column<string>(type: "TEXT", nullable: false),
                    EmailFrom = table.Column<string>(type: "TEXT", nullable: false),
                    KindleEmail = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TocSelector = table.Column<string>(type: "TEXT", nullable: false),
                    TitleSelector = table.Column<string>(type: "TEXT", nullable: false),
                    ContentSelector = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    TocUrl = table.Column<string>(type: "TEXT", nullable: false),
                    TocSelector = table.Column<string>(type: "TEXT", nullable: true),
                    TitleSelector = table.Column<string>(type: "TEXT", nullable: true),
                    ContentSelector = table.Column<string>(type: "TEXT", nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    TemplateID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Series_Templates_TemplateID",
                        column: x => x.TemplateID,
                        principalTable: "Templates",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    URL = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Crawled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Sent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Published = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeriesID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Chapters_Series_SeriesID",
                        column: x => x.SeriesID,
                        principalTable: "Series",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_SeriesID",
                table: "Chapters",
                column: "SeriesID");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_URL",
                table: "Chapters",
                column: "URL");

            migrationBuilder.CreateIndex(
                name: "IX_Series_TemplateID",
                table: "Series",
                column: "TemplateID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
