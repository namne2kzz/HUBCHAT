using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUB.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagePin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedBy",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_ChannelId_PinnedAt",
                table: "messages",
                columns: new[] { "ChannelId", "PinnedAt" },
                filter: "\"PinnedAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_messages_ChannelId_PinnedAt",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "PinnedBy",
                table: "messages");
        }
    }
}
