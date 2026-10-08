using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

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

            // Rows that existed before formats were introduced become Text. The sentinel says
            // "Text means use the column default", which is also Text, so inserts are unaffected
            // and EF does not warn that the CLR default (0) is not a real format.
            entity.Property(x => x.Format)
                .HasDefaultValue(NoteFormat.Text)
                .HasSentinel(NoteFormat.Text);

            entity.HasIndex(x => new { x.UserId, x.IsPinned, x.UpdatedAt });
            entity.HasIndex(x => new { x.UserId, x.ParentId });

            // Deleting a group deletes its sub-notes with it.
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.OwnsOne(x => x.Server, server =>
            {
                server.Property(x => x.Host).HasMaxLength(200);
                server.Property(x => x.Username).HasMaxLength(128);
                server.Property(x => x.Database).HasMaxLength(128);
            });

            // Always materialised (possibly empty), so callers never have to null-check it.
            entity.Navigation(x => x.Server).IsRequired();
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
            entity.Property(x => x.InputVariables).HasMaxLength(1000);
            entity.Property(x => x.OutputFormat).HasMaxLength(1000);
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
