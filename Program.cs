using ECommerce.Data;
using ECommerce.Services;
using ECommerce.Utils;
using FluentValidation;
using Serilog;
using System.Data;
using Npgsql;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using Sentry.AspNetCore;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configure Sentry
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Debug = builder.Configuration.GetValue<bool>("Sentry:Debug", false);
    options.TracesSampleRate = builder.Configuration.GetValue<double>("Sentry:TracesSampleRate", 1.0);
    options.Environment = builder.Environment.EnvironmentName;
    options.Release = builder.Configuration["Sentry:Release"] ?? "1.0.0";
});

// Configure Application Insights
if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    });
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ECommerce.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId:l} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Get application version
var appVersion = builder.Configuration["Application:Version"] ?? 
                 Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "1.0.0";
var appName = builder.Configuration["Application:Name"] ?? "E-Commerce API";

// Add services to the container
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = appName, 
        Version = $"v{appVersion}",
        Description = $"E-Commerce API Version {appVersion}",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "E-Commerce Support",
            Email = "TouchMunyunLLC@gmail.com"
        }
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Configuration.AddEnvironmentVariables();
// Database - Dapper with PostgreSQL
builder.Services.AddScoped<IDbConnection>(sp =>
    new Npgsql.NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IDbContext, DbContext>();

// Register Dapper type handlers
ECommerce.Data.DapperTypeHandlers.RegisterTypeHandlers();

// Redis Cache (optional — no fake localhost connection when unset)
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var redisConfigured = !string.IsNullOrWhiteSpace(redisConnection);
if (redisConfigured)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnection!));
    builder.Services.AddScoped<IRedisService, RedisService>();
}
else
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Redis must be configured in non-development environments.");
    }

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IRedisService, NoOpRedisService>();
}

// Security guards
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters long.");
}

var recaptchaSecret = builder.Configuration["Recaptcha:SecretKey"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(recaptchaSecret))
{
    throw new InvalidOperationException("Recaptcha:SecretKey must be configured in non-development environments.");
}

// AutoMapper - scan the current assembly for mapping profiles
// Note: If you're not using AutoMapper, you can remove this line
// builder.Services.AddAutoMapper(System.Reflection.Assembly.GetExecutingAssembly());

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Authentication & Authorization
var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "JWT_OR_COOKIE";
        options.DefaultChallengeScheme = "JWT_OR_COOKIE";
    })
    .AddJwtBearer("JWT", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    var cookieToken = context.Request.Cookies["authToken"];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddCookie("Cookies")
    .AddPolicyScheme("JWT_OR_COOKIE", "JWT_OR_COOKIE", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            string? authorization = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
                return "JWT";
            return "Cookies";
        };
    });

// Add OAuth providers only if configured
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = "Cookies";
    });
}



builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStripeCheckoutFulfillmentService, StripeCheckoutFulfillmentService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISMSService, SMSService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IOrderCodeService, OrderCodeService>();
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IGuestService, GuestService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISlideshowService, SlideshowService>();
builder.Services.AddScoped<IStartupService, StartupService>();
builder.Services.AddSingleton<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Register notification queue service (singleton for background processing)
builder.Services.AddSingleton<NotificationQueueService>();
builder.Services.AddSingleton<INotificationQueueService>(sp => sp.GetRequiredService<NotificationQueueService>());

// Register background service for processing notifications
builder.Services.AddHostedService<OrderNotificationBackgroundService>();
builder.Services.AddHostedService<StripeHostedCheckoutCleanupService>();

// Register HttpClient for Email and SMS services
builder.Services.AddHttpClient();

// Rate Limiting (OWASP A04 - Insecure Design)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global fixed-window policy: 100 requests per minute per IP
    options.AddPolicy("fixed", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpHelper.GetClientIpAddress(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Strict policy for auth endpoints: 10 requests per minute per IP
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpHelper.GetClientIpAddress(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Strict policy for contact/guest forms: 5 requests per minute per IP
    options.AddPolicy("strict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpHelper.GetClientIpAddress(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// X-Forwarded-For / X-Forwarded-Proto (Azure App Service, Front Door, CDNs).
var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");
if (forwardedHeadersSection.GetValue("Enabled", true))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = forwardedHeadersSection.GetValue("ForwardLimit", 2);
        if (forwardedHeadersSection.GetValue("AllowAllProxies", false))
        {
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }
    });
}

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

if (forwardedHeadersSection.GetValue("Enabled", true))
{
    app.UseForwardedHeaders();
}

// Configure the HTTP request pipeline
// Swagger restricted to Development only (OWASP A05 - Security Misconfiguration)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Commerce API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "E-Commerce API Documentation";
        c.DefaultModelsExpandDepth(-1);
    });
}

// Use Sentry tracing
app.UseSentryTracing();

// Error handling middleware (must be early in pipeline)
app.UseMiddleware<ECommerce.Utils.ErrorHandlingMiddleware>();

// Correlation ID middleware (adds X-Correlation-Id to all requests)
app.UseMiddleware<ECommerce.Utils.CorrelationIdMiddleware>();

app.UseHttpsRedirection();

// HSTS in production (OWASP A02 - Cryptographic Failures)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Initialize database, seed data, and perform startup tasks
using (var scope = app.Services.CreateScope())
{
    var autoInit = builder.Configuration.GetValue("Database:AutoInitialize", app.Environment.IsDevelopment());
    if (autoInit)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        await dbContext.InitializeDatabaseAsync();
    }

    var seedData = builder.Configuration.GetValue("Database:SeedData", app.Environment.IsDevelopment());
    if (seedData)
    {
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }

    var runStartupTasks = builder.Configuration.GetValue("Startup:Run", true);
    if (runStartupTasks)
    {
        // Perform startup tasks (cache clearing, etc.)
        var startupService = scope.ServiceProvider.GetRequiredService<IStartupService>();
        await startupService.InitializeAsync();
    }
}

app.Run();
