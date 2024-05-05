using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WSTKNG.Migrations
{
    /// <inheritdoc />
    public partial class ChapterPasswordSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderName",
                table: "Chapters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderValue",
                table: "Chapters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Chapters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderName",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "HeaderValue",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Chapters");
        }
    }
}
