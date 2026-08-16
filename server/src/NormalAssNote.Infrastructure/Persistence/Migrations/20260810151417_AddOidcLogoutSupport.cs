using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NormalAssNote.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOidcLogoutSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OidcLogoutTokenReplays",
                columns: table => new
                {
                    JtiHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OidcLogoutTokenReplays", x => x.JtiHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OidcLogoutTokenReplays_ExpiresAtUtc",
                table: "OidcLogoutTokenReplays",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OidcLogoutTokenReplays");
        }
    }
}
