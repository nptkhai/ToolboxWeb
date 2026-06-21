using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
    public DbSet<QuickLink> QuickLinks => Set<QuickLink>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<TabulatorTask> TabulatorTasks => Set<TabulatorTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Note>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.Tags).HasMaxLength(250);
            entity.HasIndex(x => new { x.UserId, x.IsPinned, x.UpdatedAt });
        });

        builder.Entity<ChecklistItem>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.UserId, x.Status, x.DueDate });
        });

        builder.Entity<FocusSession>(entity =>
        {
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => new { x.UserId, x.CompletedAt });
        });

        builder.Entity<QuickLink>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.GroupName).HasMaxLength(80);
            entity.HasIndex(x => new { x.UserId, x.GroupName, x.SortOrder });
        });

        builder.Entity<PromptTemplate>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Category, x.UpdatedAt });
        });

        builder.Entity<ActivityLog>(entity =>
        {
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Summary).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        builder.Entity<TabulatorTask>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.Budget).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.UserId, x.Status, x.DueDate });
        });
    }
}
