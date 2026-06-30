using Microsoft.EntityFrameworkCore;
using TaskApi.Web.Models;

namespace TaskApi.Web.Data;

public class TaskDbContext(DbContextOptions<TaskDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("TaskItems");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .IsRowVersion();

            entity.Property(e => e.CompletedAt)
                .HasDefaultValue(null);
        });
    }
}
