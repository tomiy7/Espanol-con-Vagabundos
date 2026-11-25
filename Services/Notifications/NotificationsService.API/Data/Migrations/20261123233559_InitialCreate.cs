using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationsService.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sent_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_notifications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sent_notifications_event_id_recipient_user_id_template",
                table: "sent_notifications",
                columns: new[] { "event_id", "recipient_user_id", "template" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sent_notifications_recipient_user_id",
                table: "sent_notifications",
                column: "recipient_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_notifications");
        }
    }
}
