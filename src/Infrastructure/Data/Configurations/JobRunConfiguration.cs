using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("job_runs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobName).HasMaxLength(100).IsRequired();
        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>();

        // Hot read-path: latest runs per job type (admin dashboard, monitoring).
        builder.HasIndex(j => new { j.JobName, j.StartedAt })
            .IsDescending(false, true);
    }
}
