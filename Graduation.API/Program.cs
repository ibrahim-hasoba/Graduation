using Shared;
using Graduation.API.Middlewares;
using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Graduation.API.Errors;

namespace Graduation.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog FIRST (before creating builder)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "EgyptianMarketplace")
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting Egyptian Marketplace API (.NET 8)");

                var builder = WebApplication.CreateBuilder(args);

                // Use Serilog
                builder.Host.UseSerilog();

                // Add services
                builder.Services.AddControllers().AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });
                builder.Services.AddEndpointsApiExplorer();

                // Configure Swagger - .NET 8 compatible version
                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {

                        Title = "Heka",
                        Version = "v1",
                        Description = "E-commerce API for Egyptian marketplace with vendor support"
                    });

                    // Add JWT Authentication to Swagger
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below."
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });

                    // Enable file upload support in Swagger
                    options.MapType<IFormFile>(() => new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    });
                });

                // Database Configuration
                builder.Services.AddDbContext<DatabaseContext>(options =>
                {
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                });

                // Identity Configuration
                builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.User.RequireUniqueEmail = true;

                    // Email verification
                    options.SignIn.RequireConfirmedEmail = true;
                    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;

                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                })
                .AddEntityFrameworkStores<DatabaseContext>()
                .AddDefaultTokenProviders();

                // JWT Configuration
                var jwtSettings = builder.Configuration.GetSection("JWTSettings");
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings["validIssuer"],
                        ValidAudience = jwtSettings["validAudience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSettings["securityKey"]!))
                    };
                });


                // Register Services
                builder.Services.AddScoped<JwtHandler>();
                builder.Services.AddScoped<IVendorService, VendorService>();
                builder.Services.AddScoped<IEmailService, EmailService>();
                builder.Services.AddScoped<IProductService, ProductService>();
                builder.Services.AddScoped<ICartService, CartService>();
                builder.Services.AddScoped<IOrderService, OrderService>();
                builder.Services.AddScoped<IReviewService, ReviewService>();
                builder.Services.AddScoped<IAdminService, AdminService>();
                builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
                builder.Services.AddScoped<IImageService, ImageService>();
                builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
                builder.Services.AddScoped<ICategoryService, CategoryService>();
                builder.Services.AddScoped<IWishlistService, WishlistService>();
                builder.Services.AddScoped<INotificationService, NotificationService>();
                builder.Services.AddScoped<IReportService, ReportService>();
                builder.Services.AddRateLimiter(options =>
                {
                    options.AddFixedWindowLimiter("fixed", opt =>
                    {
                        opt.Window = TimeSpan.FromSeconds(10);
                        opt.PermitLimit = 5; // 5 requests per 10 seconds
                        opt.QueueLimit = 2;
                    });
                });
                builder.Services.AddHostedService<TokenCleanupService>();
                // CORS Configuration
                //var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                //    ?? new[] { "http://localhost:3000", "http://localhost:4200" };

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", policy =>
                    {
                        policy
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowAnyOrigin()
                              ;
                    });
                });

                // Model Validation Configuration
                builder.Services.Configure<ApiBehaviorOptions>(options =>
                {
                    options.InvalidModelStateResponseFactory = actionContext =>
                    {
                        var errors = actionContext.ModelState
                            .Where(m => m.Value!.Errors.Count > 0)
                            .SelectMany(m => m.Value!.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToArray();

                        var errorResponse = new ApiValidationErrorResponse
                        {
                            Errors = errors
                        };

                        return new BadRequestObjectResult(errorResponse);
                    };
                });

                var app = builder.Build();

                // Seed roles and admin user
                using (var scope = app.Services.CreateScope())
                {
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                    await SeedRolesAndAdmin(roleManager, userManager);
                }

                // Configure middleware pipeline
                app.UseMiddleware<ExceptionMiddleware>();

                // Security Headers
                app.Use(async (context, next) =>
                {
                    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Append("X-Frame-Options", "DENY");
                    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
                    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
                    await next();
                });

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Graduation.API v1");
                    });
                }
                app.UseHttpsRedirection();

                // Enable static files for image uploads
                app.UseStaticFiles();

                app.UseCors("AllowAll");
                app.UseRateLimiter();
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapControllers();

                Log.Information("API started successfully on {Environment}", app.Environment.EnvironmentName);

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static async Task SeedRolesAndAdmin(RoleManager<IdentityRole> roleManager, UserManager<AppUser> userManager)
        {
            string[] roles = { "Admin", "Vendor", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminEmail = "admin@graduationapp.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var admin = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }
    }
}