using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NormalAssNote.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableOidcSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthenticationSessions",
                columns: table => new
                {
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthenticationScheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedTicket = table.Column<byte[]>(type: "bytea", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationSessions", x => x.KeyHash);
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_ApplicationUserId",
                table: "AuthenticationSessions",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_ExpiresAtUtc",
                table: "AuthenticationSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_Issuer_SessionId",
                table: "AuthenticationSessions",
                columns: new[] { "Issuer", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_Issuer_Subject",
                table: "AuthenticationSessions",
                columns: new[] { "Issuer", "Subject" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationSessions");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");
        }
    }
}
