using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tailgrab.Migrations
{
    /// <inheritdoc />
    public partial class V1101 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsIgnored",
                table: "ProfileEvaluation",
                newName: "isIgnored");

            migrationBuilder.RenameColumn(
                name: "IsIgnored",
                table: "ImageEvaluation",
                newName: "isIgnored");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "isIgnored",
                table: "ProfileEvaluation",
                newName: "IsIgnored");

            migrationBuilder.RenameColumn(
                name: "isIgnored",
                table: "ImageEvaluation",
                newName: "IsIgnored");
        }
    }
}
