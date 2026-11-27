using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroupsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_plan",
                table: "group_members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full"); // postojeci upisi: odjednom (dotadasnje jedino pravilo)

            migrationBuilder.AddColumn<string>(
                name: "payment_plan",
                table: "enrollments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full"); // postojeci upisi: odjednom (dotadasnje jedino pravilo)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_plan",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "payment_plan",
                table: "enrollments");
        }
    }
}
