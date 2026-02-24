using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireLens.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(HireLensDbContext))]
    [Migration("20260224040000_AddModelVersionTrainingMetadata")]
    public partial class AddModelVersionTrainingMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainingCategoryCount",
                table: "ModelVersions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TrainingCategoryDistribution",
                table: "ModelVersions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "TrainingSampleCount",
                table: "ModelVersions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainingCategoryCount",
                table: "ModelVersions");

            migrationBuilder.DropColumn(
                name: "TrainingCategoryDistribution",
                table: "ModelVersions");

            migrationBuilder.DropColumn(
                name: "TrainingSampleCount",
                table: "ModelVersions");
        }
    }
}
