using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Data;

public sealed class LicenseDbContext : IdentityDbContext<AdminUser>
{
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ActivationRequest> ActivationRequests => Set<ActivationRequest>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        builder.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.MachineName).HasMaxLength(120);
            e.Property(x => x.WindowsUser).HasMaxLength(120);
            e.Property(x => x.PublicKey).HasMaxLength(400).IsRequired();
            e.Property(x => x.AppVersion).HasMaxLength(40);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Customer).WithMany(x => x.Devices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ActivationRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.RequestIp).HasMaxLength(64);
            e.Property(x => x.DecisionNote).HasMaxLength(500);
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
        });

        builder.Entity<License>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.RevokeReason).HasMaxLength(500);
            e.Property(x => x.Text).IsRequired();
            e.HasOne(x => x.Device).WithMany(x => x.Licenses).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Release>(e =>
        {
            e.Property(x => x.Version).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Version).IsUnique();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.DownloadUrl).HasMaxLength(500);
        });

        builder.Entity<AuditEntry>(e =>
        {
            e.Property(x => x.Admin).HasMaxLength(120);
            e.Property(x => x.Action).HasMaxLength(60);
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.HasIndex(x => x.AtUtc);
        });
    }
}
