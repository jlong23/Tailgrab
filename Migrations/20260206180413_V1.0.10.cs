using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tailgrab.Migrations
{
    /// <inheritdoc />
    public partial class V1010 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageEvaluation",
                columns: table => new
                {
                    InventoryId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    MD5Checksum = table.Column<string>(type: "TEXT", nullable: true),
                    Evaluation = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LastDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageEvaluation", x => x.InventoryId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageEvaluation");
        }
    }
}
