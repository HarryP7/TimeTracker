using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_HasLunch_To_GeneralInfoTimeDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_lunch",
                table: "general_info_time_day",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_lunch",
                table: "general_info_time_day");
        }
    }
}
