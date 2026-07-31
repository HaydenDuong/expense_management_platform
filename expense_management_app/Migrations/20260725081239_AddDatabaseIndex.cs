using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace expense_management_app.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_NormalizedEmail",
                table: "AppUsers",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_NormalizedEmail",
                table: "AppUsers");
        }
    }
}
