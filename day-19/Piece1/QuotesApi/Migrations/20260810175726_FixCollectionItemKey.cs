using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotesApi.Migrations
{
    /// <inheritdoc />
    public partial class FixCollectionItemKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CollectionItem",
                table: "CollectionItem");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CollectionItem");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CollectionItem",
                table: "CollectionItem",
                columns: new[] { "CollectionId", "QuoteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CollectionItem",
                table: "CollectionItem");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CollectionItem",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CollectionItem",
                table: "CollectionItem",
                columns: new[] { "CollectionId", "Id" });
        }
    }
}
