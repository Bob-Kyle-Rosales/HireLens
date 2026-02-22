using HireLens.Domain.Entities;
using HireLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Infrastructure.Persistence;

public class HireLensDbContext(DbContextOptions<HireLensDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<Candidate> Candidates => Set<Candidate>();

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
    }
}
