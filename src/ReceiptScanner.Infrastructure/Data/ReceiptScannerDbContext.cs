using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Infrastructure.Data;

public class ReceiptScannerDbContext : IdentityDbContext<ApplicationUser>
{
    public ReceiptScannerDbContext(DbContextOptions<ReceiptScannerDbContext> options) : base(options)
    {
    }

    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<ReceiptItem> ReceiptItems { get; set; }
    public DbSet<Merchant> Merchants { get; set; }
    public DbSet<Settings> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Receipt entity
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.UserId).IsRequired();

            // Configure relationship with Merchant
            entity.HasOne(e => e.Merchant)
                  .WithMany(m => m.Receipts)
                  .HasForeignKey(e => e.MerchantId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Configure relationship with User
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Create index on UserId for faster queries
            entity.HasIndex(e => e.UserId);
        });

        // Configure ReceiptItem entity
        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasColumnType("decimal(10,3)"); // Support up to 9999999.999
            entity.Property(e => e.QuantityUnit).HasMaxLength(20);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.SKU).HasMaxLength(50);
            entity.Property(e => e.UserId).IsRequired();

            // Configure relationship with Receipt
            entity.HasOne(e => e.Receipt)
                  .WithMany(r => r.Items)
                  .HasForeignKey(e => e.ReceiptId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with User
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Create index on UserId for faster queries
            entity.HasIndex(e => e.UserId);
        });

        // Configure Merchant entity
        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Website).HasMaxLength(200);
            entity.Property(e => e.UserId).IsRequired();

            // Configure relationship with User
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Create index on merchant name for faster lookups
            entity.HasIndex(e => e.Name);
            
            // Create index on UserId for faster queries
            entity.HasIndex(e => e.UserId);
        });

        // Configure Settings entity
        modelBuilder.Entity<Settings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DefaultCurrencyName).IsRequired().HasMaxLength(10);
            entity.Property(e => e.DefaultCurrencySymbol).IsRequired().HasMaxLength(5);
            entity.Property(e => e.UserId).IsRequired();

            // Configure relationship with User
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Create index on UserId for faster queries
            entity.HasIndex(e => e.UserId);
        });

        // Configure BaseEntity properties for all entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseEntity.CreatedAt))
                    .HasDefaultValueSql("GETUTCDATE()");
            }
        }
    }
}