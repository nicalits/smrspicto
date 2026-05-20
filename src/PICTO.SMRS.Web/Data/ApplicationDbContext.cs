using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryItemSerial> InventoryItemSerials => Set<InventoryItemSerial>();
    public DbSet<RequisitionRecord> RequisitionRecords => Set<RequisitionRecord>();
    public DbSet<RequisitionRecordItem> RequisitionRecordItems => Set<RequisitionRecordItem>();
    public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();
    public DbSet<BorrowRecordItem> BorrowRecordItems => Set<BorrowRecordItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // 2FA is not used; drop the column from AspNetUsers (see migration).
        builder.Entity<IdentityUser>().Ignore(u => u.TwoFactorEnabled);

        builder.Entity<InventoryItem>(e =>
        {
            e.Property(x => x.ItemName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(128);
            e.Property(x => x.Location).HasMaxLength(256);
            e.Property(x => x.Description).HasColumnType("nvarchar(max)");
            e.Property(x => x.Specifications).HasMaxLength(4000);
            e.Property(x => x.ImagePath).HasMaxLength(512);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.ItemName);
            e.HasIndex(x => x.Brand);
        });

        builder.Entity<InventoryItemSerial>(e =>
        {
            e.Property(x => x.SerialNumber).HasMaxLength(256).IsRequired();
            e.HasIndex(x => new { x.InventoryItemId, x.SerialNumber }).IsUnique();
            e.HasOne(x => x.InventoryItem)
                .WithMany(i => i.Serials)
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RequisitionRecord>(e =>
        {
            e.Property(x => x.RsNo).HasMaxLength(20);
            e.Property(x => x.RequestorUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.RequestorName).HasMaxLength(200).IsRequired();
            e.Property(x => x.RequestorPosition).HasMaxLength(200).IsRequired();
            e.Property(x => x.RequestorDivision).HasMaxLength(200).IsRequired();
            e.Property(x => x.Office).HasMaxLength(200);
            e.Property(x => x.MrIcsPosition).HasMaxLength(200);
            e.Property(x => x.PendingReason).HasMaxLength(500);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.ActionedByUserId).HasMaxLength(450);
            e.HasIndex(x => new { x.ItemType, x.Status, x.Date });
            e.HasIndex(x => x.RequestorUserId);
        });

        builder.Entity<RequisitionRecordItem>(e =>
        {
            e.Property(x => x.SerialNo).HasMaxLength(200);
            e.Property(x => x.Unit).HasMaxLength(50).IsRequired();
            e.Property(x => x.Purpose).HasMaxLength(300).IsRequired();
            e.Property(x => x.RfNo).HasMaxLength(100);
            e.HasOne(x => x.RequisitionRecord)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.RequisitionRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BorrowRecord>(e =>
        {
            e.Property(x => x.RfNo).HasMaxLength(20);
            e.Property(x => x.BorrowerUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.BorrowerName).HasMaxLength(200).IsRequired();
            e.Property(x => x.BorrowerDivision).HasMaxLength(200).IsRequired();
            e.Property(x => x.Office).HasMaxLength(200);
            e.Property(x => x.SlipTime).HasMaxLength(20);
            e.Property(x => x.TelNo).HasMaxLength(50);
            e.Property(x => x.Remarks).HasMaxLength(2000);
            e.Property(x => x.PendingReason).HasMaxLength(500);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.ActionedByUserId).HasMaxLength(450);
            e.Property(x => x.ReturnConfirmedByUserId).HasMaxLength(450);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.BorrowerUserId);
        });

        builder.Entity<BorrowRecordItem>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.LocationVenue).HasMaxLength(300).IsRequired();
            e.Property(x => x.Purpose).HasMaxLength(300).IsRequired();
            e.Property(x => x.BorrowTime).HasMaxLength(20);
            e.Property(x => x.ReturnTime).HasMaxLength(20);
            e.HasOne(x => x.BorrowRecord)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.BorrowRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
