using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Graduation.DAL.Data
{
    public class DatabaseContext : IdentityDbContext<AppUser>
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
        }

        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Vendor Configuration
            builder.Entity<Vendor>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.HasOne(v => v.User)
                    .WithMany()
                    .HasForeignKey(v => v.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => v.StoreName).IsUnique();
                entity.Property(v => v.StoreName).IsRequired().HasMaxLength(200);
                entity.Property(v => v.PhoneNumber).IsRequired().HasMaxLength(20);

                // One-to-One: one user = one vendor store
                entity.HasIndex(v => v.UserId).IsUnique();

                // Performance Indexes
                entity.HasIndex(v => v.IsApproved);
                entity.HasIndex(v => v.IsActive);
                entity.HasIndex(v => new { v.IsApproved, v.IsActive });
                entity.HasIndex(v => v.Governorate);
                entity.HasIndex(v => v.CreatedAt);

                entity.Property(v => v.Code)
                    .HasMaxLength(12)
                    .IsRequired(false);

                entity.HasIndex(v => v.Code)
                 .IsUnique()
                 .HasFilter("[Code] IS NOT NULL");
            });

            builder.Entity<AppUser>(b =>
            {
                b.Property(u => u.Code)
                 .HasMaxLength(12)
                 .IsRequired(false);

                b.HasIndex(u => u.Code)
                 .IsUnique()
                 .HasFilter("[Code] IS NOT NULL");
            });

            // Category Configuration
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.ParentCategory)
                    .WithMany(c => c.SubCategories)
                    .HasForeignKey(c => c.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
                entity.Property(c => c.NameEn).IsRequired().HasMaxLength(200);

                // Performance Indexes
                entity.HasIndex(c => c.IsActive);
                entity.HasIndex(c => c.ParentCategoryId);
                entity.HasIndex(c => new { c.IsActive, c.ParentCategoryId });
            });

            // Product Configuration
            builder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Code)
                    .HasMaxLength(12)
                    .IsRequired(false);

                entity.HasIndex(p => p.Code)
                 .IsUnique()
                 .HasFilter("[Code] IS NOT NULL");

                entity.HasKey(p => p.Id);

                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.DiscountPrice).HasPrecision(18, 2);
                entity.Property(p => p.NameAr).IsRequired().HasMaxLength(300);
                entity.Property(p => p.NameEn).IsRequired().HasMaxLength(300);
                entity.Property(p => p.SKU).IsRequired().HasMaxLength(100);

                entity.HasOne(p => p.Vendor)
                    .WithMany(v => v.Products)
                    .HasForeignKey(p => p.VendorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.SKU).IsUnique();

                // Performance Indexes for Search and Filtering
                entity.HasIndex(p => p.IsActive);
                entity.HasIndex(p => p.CategoryId);
                entity.HasIndex(p => p.VendorId);
                entity.HasIndex(p => p.Price);
                entity.HasIndex(p => p.IsFeatured);
                entity.HasIndex(p => p.IsEgyptianMade);
                entity.HasIndex(p => p.StockQuantity);
                entity.HasIndex(p => p.CreatedAt);
                entity.HasIndex(p => p.ViewCount);

                // Composite Indexes for Common Queries
                entity.HasIndex(p => new { p.IsActive, p.CategoryId });
                entity.HasIndex(p => new { p.IsActive, p.VendorId });
                entity.HasIndex(p => new { p.IsActive, p.IsFeatured });
                entity.HasIndex(p => new { p.IsActive, p.Price });
                entity.HasIndex(p => new { p.CategoryId, p.IsActive, p.Price });
                entity.HasIndex(p => new { p.VendorId, p.IsActive, p.CreatedAt });
            });

            // Product Image Configuration
            builder.Entity<ProductImage>(entity =>
            {
                entity.HasKey(pi => pi.Id);

                entity.HasOne(pi => pi.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(pi => pi.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(pi => pi.ImageUrl).IsRequired();

                // Performance Indexes
                entity.HasIndex(pi => pi.ProductId);
                entity.HasIndex(pi => new { pi.ProductId, pi.IsPrimary });
                entity.HasIndex(pi => new { pi.ProductId, pi.DisplayOrder });
            });

            // UserAddress Configuration
            builder.Entity<UserAddress>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Nickname)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.FullAddress)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(e => e.Latitude)
                      .HasColumnType("float");

                entity.Property(e => e.Longitude)
                      .HasColumnType("float");

                // Unique filtered index: only one IsDefault=true per user
                entity.HasIndex(e => new { e.UserId, e.IsDefault })
                      .HasFilter("[IsDefault] = 1")
                      .IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Product Review Configuration
            builder.Entity<ProductReview>(entity =>
            {
                entity.HasKey(pr => pr.Id);

                entity.HasOne(pr => pr.Product)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(pr => pr.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pr => pr.User)
                    .WithMany()
                    .HasForeignKey(pr => pr.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(pr => pr.Rating).IsRequired();

                // Performance Indexes
                entity.HasIndex(pr => pr.ProductId);
                entity.HasIndex(pr => pr.UserId);
                entity.HasIndex(pr => pr.IsApproved);
                entity.HasIndex(pr => pr.CreatedAt);
                entity.HasIndex(pr => new { pr.ProductId, pr.IsApproved });
                entity.HasIndex(pr => new { pr.UserId, pr.ProductId }).IsUnique();
            });

            // Cart Item Configuration
            builder.Entity<CartItem>(entity =>
            {
                entity.HasKey(ci => ci.Id);

                entity.HasOne(ci => ci.User)
                    .WithMany()
                    .HasForeignKey(ci => ci.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ci => ci.Product)
                    .WithMany(p => p.CartItems)
                    .HasForeignKey(ci => ci.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ci => new { ci.UserId, ci.ProductId }).IsUnique();

                // Performance Indexes
                entity.HasIndex(ci => ci.UserId);
                entity.HasIndex(ci => ci.ProductId);
                entity.HasIndex(ci => ci.AddedAt);
            });

            // Global query filters — only applied to entities where hiding inactive data
            // in the current session makes sense for ALL callers.
            //
            // FIX #12 side-effect: Products are now soft-deleted (IsActive = false).
            // ProductImages, ProductReviews, and CartItems filter by Product.IsActive —
            // this is safe because:
            //   - Images/reviews of a deleted product should not surface in the storefront.
            //   - Cart items for a deleted product should not be purchasable.
            //
            // IMPORTANT: OrderItems must NOT have a global filter on Product.IsActive.
            // A soft-deleted product must not erase order history. Any code that reads
            // OrderItems for historical purposes (reports, order detail pages, review
            // eligibility checks) must use .IgnoreQueryFilters() explicitly if it also
            // needs to traverse the Product navigation — or simply avoid filtering
            // OrderItems by product activity at the global level.
            builder.Entity<ProductImage>().HasQueryFilter(pi => pi.Product.IsActive);
            builder.Entity<ProductReview>().HasQueryFilter(pr => pr.Product.IsActive);
            builder.Entity<CartItem>().HasQueryFilter(ci => ci.Product.IsActive);
            // NOTE: No global filter on OrderItem — order history must be preserved.

            // RefreshToken Configuration
            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(rt => rt.Id);

                entity.HasOne(rt => rt.User)
                    .WithMany()
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(rt => rt.Token).IsRequired().HasMaxLength(200);

                // Performance Indexes
                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasIndex(rt => rt.UserId);
                entity.HasIndex(rt => rt.ExpiresAt);
                entity.HasIndex(rt => rt.IsRevoked);
                entity.HasIndex(rt => new { rt.Token, rt.UserId, rt.IsRevoked });
                entity.HasIndex(rt => new { rt.UserId, rt.IsRevoked, rt.ExpiresAt });
            });

            // Order Configuration
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.Property(o => o.SubTotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingCost).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
                entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(o => o.ShippingPhone).IsRequired().HasMaxLength(20);

                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(o => o.OrderNumber).IsUnique();

                // Performance Indexes
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.Status);
                entity.HasIndex(o => o.PaymentStatus);
                entity.HasIndex(o => o.OrderDate);
                entity.HasIndex(o => o.ShippingGovernorate);

                // Composite Indexes for Common Queries
                entity.HasIndex(o => new { o.UserId, o.OrderDate });
                entity.HasIndex(o => new { o.Status, o.OrderDate });
                entity.HasIndex(o => new { o.UserId, o.Status, o.OrderDate });
            });

            // Order Item Configuration
            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);

                entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
                entity.Property(oi => oi.TotalPrice).HasPrecision(18, 2);

                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.Product)
                    .WithMany(p => p.OrderItems)
                    .HasForeignKey(oi => oi.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Performance Indexes
                entity.HasIndex(oi => oi.OrderId);
                entity.HasIndex(oi => oi.ProductId);
            });

            // Wishlist Configuration
            builder.Entity<Wishlist>(entity =>
            {
                entity.HasKey(w => w.Id);

                entity.HasOne(w => w.User)
                    .WithMany()
                    .HasForeignKey(w => w.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(w => w.Product)
                    .WithMany()
                    .HasForeignKey(w => w.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one product per user in wishlist
                entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

                // Performance Indexes
                entity.HasIndex(w => w.UserId);
                entity.HasIndex(w => w.ProductId);
                entity.HasIndex(w => w.CreatedAt);
            });

            // Notification Configuration
            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Type).IsRequired().HasMaxLength(50);

                // Performance Indexes
                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.IsRead);
                entity.HasIndex(n => n.CreatedAt);
                entity.HasIndex(n => new { n.UserId, n.IsRead });
                entity.HasIndex(n => new { n.UserId, n.CreatedAt });
            });

            // Email OTP Configuration
            builder.Entity<EmailOtp>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => new { e.Email, e.Purpose });
                entity.HasIndex(e => e.ExpiresAt);
            });

            builder.Entity<Category>(b =>
            {
                b.Property(c => c.Code)
                 .HasMaxLength(12)
                 .IsRequired(false);

                b.HasIndex(c => c.Code)
                 .IsUnique()
                 .HasFilter("[Code] IS NOT NULL");
            });
        }
    }
}
