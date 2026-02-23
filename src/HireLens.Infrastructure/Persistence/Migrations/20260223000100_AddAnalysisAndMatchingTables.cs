using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisAndMatchingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Accuracy = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TrainedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PredictedCategory = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    ExtractedSkills = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnalyzedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeAnalyses_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResumeAnalyses_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchScore = table.Column<double>(type: "float", nullable: false),
                    MatchedSkills = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MissingSkills = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TopOverlappingKeywords = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    GeneratedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchResults_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchResults_ResumeAnalyses_ResumeAnalysisId",
                        column: x => x.ResumeAnalysisId,
                        principalTable: "ResumeAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_CandidateId",
                table: "MatchResults",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_GeneratedUtc",
                table: "MatchResults",
                column: "GeneratedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_JobPostingId",
                table: "MatchResults",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_JobPostingId_MatchScore",
                table: "MatchResults",
                columns: new[] { "JobPostingId", "MatchScore" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_ResumeAnalysisId",
                table: "MatchResults",
                column: "ResumeAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelType_IsActive",
                table: "ModelVersions",
                columns: new[] { "ModelType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelType_Version",
                table: "ModelVersions",
                columns: new[] { "ModelType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_TrainedUtc",
                table: "ModelVersions",
                column: "TrainedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_AnalyzedUtc",
                table: "ResumeAnalyses",
                column: "AnalyzedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_CandidateId",
                table: "ResumeAnalyses",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_ModelVersionId",
                table: "ResumeAnalyses",
                column: "ModelVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropTable(
                name: "ResumeAnalyses");

            migrationBuilder.DropTable(
                name: "ModelVersions");
        }
    }
}
