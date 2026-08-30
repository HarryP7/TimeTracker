using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_GeneralInfoTimeDay_DbTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "general_info_time_day",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    work_start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_pause_seconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_info_time_day", x => x.date);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "general_info_time_day");
        }
    }
}
