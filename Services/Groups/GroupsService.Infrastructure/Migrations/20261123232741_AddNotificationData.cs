using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroupsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "course_id",
                table: "partner_invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "inviter_user_id",
                table: "partner_invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "group_members",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Postojeci redovi: podaci se prepisuju iz upisa na koje vec pokazuju.
            migrationBuilder.Sql("""
                UPDATE group_members gm SET user_id = e.user_id
                FROM enrollments e WHERE e.id = gm.enrollment_id;

                UPDATE partner_invitations pi SET inviter_user_id = e.user_id, course_id = e.course_id
                FROM enrollments e WHERE e.id = pi.inviter_enrollment_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "course_id",
                table: "partner_invitations");

            migrationBuilder.DropColumn(
                name: "inviter_user_id",
                table: "partner_invitations");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "group_members");
        }
    }
}
