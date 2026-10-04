using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUB.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_channels_WorkspaceId_LinkExternalId",
                table: "channels");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_AuthorId_ClientMessageId",
                table: "messages",
                columns: new[] { "AuthorId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");

            // Before the race was closed, two concurrent opens could create two threads for one resource.
            // Those duplicates would make the unique index below fail — and services apply migrations on
            // startup, so the service would not boot. Keep the oldest thread linked and unlink the rest:
            // they stay as ordinary channels with all their messages; only the link metadata is dropped.
            // Not reversible by Down (the dropped links are not recorded).
            migrationBuilder.Sql("""
                UPDATE channels c
                SET "LinkType" = NULL, "LinkExternalId" = NULL, "LinkExternalKey" = NULL, "LinkUrl" = ''
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "WorkspaceId", "LinkType", "LinkExternalId"
                        ORDER BY "CreatedAt", "Id") AS rn
                    FROM channels
                    WHERE "LinkExternalId" IS NOT NULL
                ) d
                WHERE c."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_channels_WorkspaceId_LinkType_LinkExternalId",
                table: "channels",
                columns: new[] { "WorkspaceId", "LinkType", "LinkExternalId" },
                unique: true,
                filter: "\"LinkExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_messages_AuthorId_ClientMessageId",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_channels_WorkspaceId_LinkType_LinkExternalId",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "IX_channels_WorkspaceId_LinkExternalId",
                table: "channels",
                columns: new[] { "WorkspaceId", "LinkExternalId" });
        }
    }
}
