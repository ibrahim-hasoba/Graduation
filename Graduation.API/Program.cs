using FluentValidation;
using Graduation.API.Errors;
using Graduation.API.Extensions;
using Graduation.API.Filters;
using Graduation.API.HostedServices;
using Graduation.API.Middlewares;
using Graduation.API.Swagger;
using Graduation.API.Swagger.Filters;
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
using Shared;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace Graduation.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Heka")
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting Heka");

                var builder = WebApplication.CreateBuilder(args);

                var jwtKey = builder.Configuration["JWTSettings:securityKey"];
                if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
                    throw new InvalidOperationException(
                        "JWTSettings:securityKey is missing or too short (minimum 32 characters). " +
                        "Set it via environment variable or dotnet user-secrets.");

                var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException(
                        "ConnectionStrings:DefaultConnection is missing. " +
                        "Set it via environment variable ConnectionStrings__DefaultConnection.");

                builder.Host.UseSerilog();


                builder.Services.AddValidatorsFromAssemblies(Assembly.GetExecutingAssembly());

                builder.Services.AddControllers(options =>
                {
                    options.Filters.Add<FluentValidationFilter>();
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

                builder.Services.Configure<ApiBehaviorOptions>(options =>
                {
                    options.SuppressModelStateInvalidFilter = true;

                    options.InvalidModelStateResponseFactory = actionContext =>
                    {
                        var errors = actionContext.ModelState
                            .Where(m => m.Value!.Errors.Count > 0)
                            .SelectMany(m => m.Value!.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToArray();

                        return new BadRequestObjectResult(new ApiValidationErrorResponse { Errors = errors });
                    };
                });

                builder.Services.AddEndpointsApiExplorer();

                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {

                        Title = "Heka",
                        Version = "v1",
                        Description = "Heka API"
                    });

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

                    options.MapType<IFormFile>(() => new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    });

                    try
                    {
                        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                        if (File.Exists(xmlPath))
                        {
                            options.IncludeXmlComments(xmlPath);
                        }
                    }
                    catch { }

                    try
                    {
                        options.OperationFilter<ApiResponseOperationFilter>();
                        options.OperationFilter<ExampleOperationFilter>();
                    }
                    catch { }
                });

                builder.Services.AddDbContext<DatabaseContext>(options =>
                {
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                });

                builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.User.RequireUniqueEmail = true;

                    options.SignIn.RequireConfirmedEmail = true;
                    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;

                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                })
                .AddEntityFrameworkStores<DatabaseContext>()
                .AddDefaultTokenProviders();

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
                builder.Services.AddScoped<IOtpService, OtpService>();
                builder.Services.AddScoped<IWishlistService, WishlistService>();
                builder.Services.AddScoped<INotificationService, NotificationService>();
                builder.Services.AddScoped<IReportService, ReportService>();
                builder.Services.AddAuthorization();
                builder.Services.AddRateLimiter(options =>
                {
                    options.AddFixedWindowLimiter("fixed", opt =>
                    {
                        opt.Window = TimeSpan.FromSeconds(10);
                        opt.PermitLimit = 5; 
                        opt.QueueLimit = 2;
                    });
                    options.AddFixedWindowLimiter("otp", opt =>
                    {
                        opt.Window = TimeSpan.FromMinutes(60);
                        opt.PermitLimit = 30; 
                        opt.QueueLimit = 0;
                    });
                });
                builder.Services.AddSingleton<Shared.BackgroundTasks.IBackgroundTaskQueue,
                    Graduation.API.BackgroundTasks.BackgroundTaskQueue>();
                builder.Services.AddHostedService<BackgroundProcessingService>();
                builder.Services.AddHostedService<TokenCleanupService>();
                builder.Services.AddHostedService<ClearOldNotificationsHostedService>();

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

                var app = builder.Build();

                using (var scope = app.Services.CreateScope())
                {
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                    await SeedRolesAndAdmin(roleManager, userManager);
                }

                app.UseMiddleware<ExceptionMiddleware>();
                if (app.Environment.IsProduction())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Heka.API v1");
                    });
                }
                app.UseHttpsRedirection();
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