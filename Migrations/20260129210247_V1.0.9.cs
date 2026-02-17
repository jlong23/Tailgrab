using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tailgrab.Migrations
{
    /// <inheritdoc />
    public partial class V109 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvatarInfo",
                columns: table => new
                {
                    AvatarId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    AvatarName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsBOS = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvatarInfo", x => x.AvatarId);
                });

            migrationBuilder.CreateTable(
                name: "GroupInfo",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "TEXT", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", nullable: true),
                    IsBOS = table.Column<bool>(type: "INTEGER", nullable: false),
                    createDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updateDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInfo", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "ProfileEvaluation",
                columns: table => new
                {
                    MD5Checksum = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileText = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Evaluation = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LastDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileEvaluation", x => x.MD5Checksum);
                });

            migrationBuilder.CreateTable(
                name: "UserInfo",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    elapsedHours = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsBOS = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInfo", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvatarInfo");

            migrationBuilder.DropTable(
                name: "GroupInfo");

            migrationBuilder.DropTable(
                name: "ProfileEvaluation");

            migrationBuilder.DropTable(
                name: "UserInfo");
        }
    }
}
