using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Syncfusion.Licensing;
using System.Data;
using System.Text;
using ticketing_system_backend.API.Interfaces;
using ticketing_system_backend.API.Repositories;
using ticketing_system_backend.Helpers;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Middleware;
using ticketing_system_backend.Services;
using ticketing_system_backend.Settings;
using ticketing_system_backend.Repository;

SyncfusionLicenseProvider.RegisterLicense("NxYtFisQPR08Cit/Vkd+XU9FcVRDX3xKf0x/TGpQb19xflBPallYVBYiSV9jS3tSdkVlWXtecHBWR2hbUk91Xg==");

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
// 1. CONFIGURATION — strongly-typed settings
// ═══════════════════════════════════════════════════════════════════════
EmailTemplates.BaseUrl = builder.Configuration["FrontendBaseUrl"] ?? "http://localhost:4200";

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured in appsettings.json");

// ═══════════════════════════════════════════════════════════════════════
// 2. CORE SERVICES
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();

// ═══════════════════════════════════════════════════════════════════════
// 3. SWAGGER — with JWT Bearer auth button
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ticketing API", Version = "v1" });
    c.CustomSchemaIds(type => type.FullName);

    // Allow sending Bearer token in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT access token (without 'Bearer ' prefix)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ═══════════════════════════════════════════════════════════════════════
// 4. CORS — specific origins + credentials (required for httpOnly cookie)
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:4200" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();   // Required for httpOnly cookie (refresh token)
    });
});

// ═══════════════════════════════════════════════════════════════════════
// 5. JWT AUTHENTICATION
// ═══════════════════════════════════════════════════════════════════════
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata      = false;       // true in production
    options.SaveToken                 = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = signingKey,
        ValidateIssuer           = true,
        ValidIssuer              = jwtSettings.Issuer,
        ValidateAudience         = true,
        ValidAudience            = jwtSettings.Audience,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero     // No grace period — expires exactly on time
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode  = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                """{"success":false,"code":"UNAUTHORIZED","message":"Authentication required. Please login.","status":401}""");
        },
        OnForbidden = async ctx =>
        {
            ctx.Response.StatusCode  = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                """{"success":false,"code":"FORBIDDEN","message":"You do not have permission to access this resource.","status":403}""");
        }
    };
});

// Role-based authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PmOnly",       p => p.RequireRole("PM"))
    .AddPolicy("CustomerOnly", p => p.RequireRole("Customer"))
    .AddPolicy("DevOnly",      p => p.RequireRole("Assignee"))
    .AddPolicy("TeamOnly",     p => p.RequireRole("PM", "Assignee"));

// ═══════════════════════════════════════════════════════════════════════
// 6. APPLICATION SERVICES
// ═══════════════════════════════════════════════════════════════════════
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationSchedulerService>();

builder.Services.AddScoped<IDbConnection>(sp => 
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("DefaultConnection connection string is not configured.")));

builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IManager, ManagerRepository>();
builder.Services.AddScoped<IRolesRepository, RolesRepository>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Email service
builder.Services.AddSingleton<IEmailService>(provider =>
{
    var email = provider.GetRequiredService<IConfiguration>().GetSection("EmailSettings");
    var host  = email["Host"]    ?? throw new InvalidOperationException("EmailSettings:Host missing.");
    var from  = email["FromAddress"] ?? throw new InvalidOperationException("EmailSettings:FromAddress missing.");
    var pwd   = email["Password"]    ?? throw new InvalidOperationException("EmailSettings:Password missing.");
    if (!int.TryParse(email["Port"], out var port)) throw new InvalidOperationException("EmailSettings:Port invalid.");
    return new SmtpEmailService(host, port, from, pwd);
});

// Logging
builder.Services.AddLogging(logging => { logging.AddConsole(); logging.AddDebug(); });

// ═══════════════════════════════════════════════════════════════════════
// 7. BUILD + MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════════════════════════
var app = builder.Build();

// CORS must be before auth
app.UseCors("AllowAngularDev");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticketing API v1");
    c.DisplayRequestDuration();
});

// ── Auth pipeline (order matters!) ───────────────────────────────────
app.UseJwtMiddleware();      // 1. Custom: enrich context + JSON errors
app.UseAuthentication();     // 2. Built-in: validate token → HttpContext.User
app.UseAuthorization();      // 3. Built-in: enforce [Authorize] attributes

app.MapHub<ticketing_system_backend.Hubs.NotificationHub>("/notificationHub");
app.MapControllers();

// ═══════════════════════════════════════════════════════════════════════
// 8. STARTUP CHECKS
// ═══════════════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var connStr = config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        logger.LogInformation("Database connection successful");

        var uploadPath = config.GetValue<string>("FileUploadPath")
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
            logger.LogInformation("Created upload directory: {Path}", uploadPath);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup check failed");
    }
}

app.Run();
// Trigger watch restart

