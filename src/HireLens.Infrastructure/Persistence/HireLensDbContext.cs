using HireLens.Domain.Entities;
using HireLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Infrastructure.Persistence;

public class HireLensDbContext(DbContextOptions<HireLensDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<ResumeAnalysis> ResumeAnalyses => Set<ResumeAnalysis>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<JobPosting>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(12000).IsRequired();
            entity.Property(x => x.RequiredSkills).HasMaxLength(2000);
            entity.Property(x => x.OptionalSkills).HasMaxLength(2000);
            entity.Property(x => x.SeniorityLevel).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.CreatedUtc);
        });

        builder.Entity<Candidate>(entity =>
        {
            entity.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ResumeFileName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ResumeContentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ResumeText).HasMaxLength(200_000).IsRequired();
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.CreatedUtc);
        });

        builder.Entity<ModelVersion>(entity =>
        {
            entity.Property(x => x.Version).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModelType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.TrainingCategoryDistribution).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.ModelType, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.ModelType, x.IsActive });
            entity.HasIndex(x => x.TrainedUtc);
        });

        builder.Entity<ResumeAnalysis>(entity =>
        {
            entity.Property(x => x.PredictedCategory).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ExtractedSkills).HasMaxLength(4000).IsRequired();
            entity.HasOne<Candidate>()
                .WithMany()
                .HasForeignKey(x => x.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ModelVersion>()
                .WithMany()
                .HasForeignKey(x => x.ModelVersionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.CandidateId);
            entity.HasIndex(x => x.AnalyzedUtc);
        });

        builder.Entity<MatchResult>(entity =>
        {
            entity.Property(x => x.MatchedSkills).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.MissingSkills).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.TopOverlappingKeywords).HasMaxLength(2000).IsRequired();
            entity.HasOne<JobPosting>()
                .WithMany()
                .HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Candidate>()
                .WithMany()
                .HasForeignKey(x => x.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ResumeAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.ResumeAnalysisId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.JobPostingId);
            entity.HasIndex(x => x.CandidateId);
            entity.HasIndex(x => x.GeneratedUtc);
            entity.HasIndex(x => new { x.JobPostingId, x.MatchScore });
        });
    }
}
