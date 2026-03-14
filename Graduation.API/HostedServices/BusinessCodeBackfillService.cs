using Graduation.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Utilities;

namespace Graduation.API.HostedServices
{
    public class BusinessCodeBackfillService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BusinessCodeBackfillService> _logger;

        public BusinessCodeBackfillService(
            IServiceProvider serviceProvider,
            ILogger<BusinessCodeBackfillService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            await BackfillUsersAsync(context, cancellationToken);
            await BackfillProductsAsync(context, cancellationToken);
            await BackfillVendorsAsync(context, cancellationToken);
            await BackfillCategoriesAsync(context, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;


        private async Task BackfillUsersAsync(DatabaseContext ctx, CancellationToken ct)
        {
            var users = await ctx.Users
                .Where(u => u.Code == null)
                .ToListAsync(ct);

            if (!users.Any()) return;

            _logger.LogInformation("Backfilling business codes for {Count} user(s)…", users.Count);
            foreach (var u in users)
                u.Code = BusinessCodeGenerator.ForUser(u.Id);

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("User code backfill complete.");
        }

        private async Task BackfillProductsAsync(DatabaseContext ctx, CancellationToken ct)
        {
            var products = await ctx.Products
                .Where(p => p.Code == null)
                .ToListAsync(ct);

            if (!products.Any()) return;

            _logger.LogInformation("Backfilling business codes for {Count} product(s)…", products.Count);
            foreach (var p in products)
                p.Code = BusinessCodeGenerator.ForProduct(p.Id);

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("Product code backfill complete.");
        }

        private async Task BackfillVendorsAsync(DatabaseContext ctx, CancellationToken ct)
        {
            var vendors = await ctx.Vendors
                .Where(v => v.Code == null)
                .ToListAsync(ct);

            if (!vendors.Any()) return;

            _logger.LogInformation("Backfilling business codes for {Count} vendor(s)…", vendors.Count);
            foreach (var v in vendors)
                v.Code = BusinessCodeGenerator.ForVendor(v.Id);

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("Vendor code backfill complete.");
        }

        private async Task BackfillCategoriesAsync(DatabaseContext ctx, CancellationToken ct)
        {
            var categories = await ctx.Categories
                .Where(c => c.Code == null)
                .ToListAsync(ct);

            if (!categories.Any()) return;

            _logger.LogInformation("Backfilling business codes for {Count} categor(ies)…", categories.Count);
            foreach (var c in categories)
                c.Code = BusinessCodeGenerator.ForCategory(c.Id);

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("Category code backfill complete.");
        }
    }
}
