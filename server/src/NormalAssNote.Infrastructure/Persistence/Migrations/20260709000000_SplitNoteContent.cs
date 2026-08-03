using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NormalAssNote.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitNoteContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteContents",
                columns: table => new
                {
                    NoteId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteContents", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_NoteContents_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "NoteContents" ("NoteId", "Content")
                SELECT "Id", "Content"
                FROM "Notes";
                """);

            migrationBuilder.DropColumn(
                name: "Content",
                table: "Notes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Notes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Notes"
                SET "Content" = COALESCE(contents."Content", '')
                FROM "NoteContents" AS contents
                WHERE contents."NoteId" = "Notes"."Id";
                """);

            migrationBuilder.DropTable(
                name: "NoteContents");
        }
    }
}
