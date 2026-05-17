using Graduation.API.Extensions;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILanguageService Lang;

        protected BaseController(ILanguageService lang)
        {
            Lang = lang;
        }

        protected string GetRequiredUserId()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.NotAuthenticated));
            return userId;
        }

        protected async Task<Shared.DTOs.Vendor.VendorDto> GetCurrentVendorAsync(IVendorService vendorService)
        {
            var userId = GetRequiredUserId();
            var vendor = await vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));
            return vendor;
        }

        protected async Task<T> ExecuteInTransactionAsync<T>(DatabaseContext context, Func<Task<T>> action)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var result = await action();
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        protected async Task ExecuteInTransactionAsync(DatabaseContext context, Func<Task> action)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    await action();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        protected IActionResult OkResult(object? data = null, string? message = null, int? count = null)
            => Ok(new Errors.ApiResult(data, message, count));

        protected IActionResult CreatedResult(object? data = null, string? message = null)
            => StatusCode(201, new Errors.ApiResult(data, message));

        protected object PaginatedResponse<T>(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
        {
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return new
            {
                items,
                totalCount,
                pageNumber,
                pageSize,
                totalPages,
                hasPreviousPage = pageNumber > 1,
                hasNextPage = pageNumber < totalPages
            };
        }
    }
}
