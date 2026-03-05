using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tailgrab.Migrations
{
    /// <inheritdoc />
    public partial class V110 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBOS",
                table: "UserInfo");

            migrationBuilder.RenameColumn(
                name: "elapsedHours",
                table: "UserInfo",
                newName: "ElapsedMinutes");

            migrationBuilder.RenameColumn(
                name: "IsBOS",
                table: "GroupInfo",
                newName: "AlertType");

            migrationBuilder.RenameColumn(
                name: "IsBOS",
                table: "AvatarInfo",
                newName: "AlertType");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateJoined",
                table: "UserInfo",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "LastProfileChecksum",
                table: "UserInfo",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIgnored",
                table: "ProfileEvaluation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIgnored",
                table: "ImageEvaluation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "AvatarInfo",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateJoined",
                table: "UserInfo");

            migrationBuilder.DropColumn(
                name: "LastProfileChecksum",
                table: "UserInfo");

            migrationBuilder.DropColumn(
                name: "IsIgnored",
                table: "ProfileEvaluation");

            migrationBuilder.DropColumn(
                name: "IsIgnored",
                table: "ImageEvaluation");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "AvatarInfo");

            migrationBuilder.RenameColumn(
                name: "ElapsedMinutes",
                table: "UserInfo",
                newName: "elapsedHours");

            migrationBuilder.RenameColumn(
                name: "AlertType",
                table: "GroupInfo",
                newName: "IsBOS");

            migrationBuilder.RenameColumn(
                name: "AlertType",
                table: "AvatarInfo",
                newName: "IsBOS");

            migrationBuilder.AddColumn<int>(
                name: "IsBOS",
                table: "UserInfo",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
