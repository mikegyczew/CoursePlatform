using System.ComponentModel.DataAnnotations;
using System.Text;
using Backend.Data;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var superAdminAliases = new Dictionary<string, string?>();
if (
    string.IsNullOrWhiteSpace(builder.Configuration["SuperAdmin:Email"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["SUPERADMIN_EMAIL"])
)
{
    superAdminAliases["SuperAdmin:Email"] =
        builder.Configuration["SUPERADMIN_EMAIL"];
}

if (
    string.IsNullOrWhiteSpace(builder.Configuration["SuperAdmin:Password"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["SUPERADMIN_PASSWORD"])
)
{
    superAdminAliases["SuperAdmin:Password"] =
        builder.Configuration["SUPERADMIN_PASSWORD"];
}

if (superAdminAliases.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(superAdminAliases);
}

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<CourseDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' was not found."
        )
    )
);

builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LessonProgressService>();
builder.Services.AddScoped<DropboxLessonProgressService>();
builder.Services.AddScoped<DropboxCouponService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmailService, MailjetEmailService>();
builder.Services.AddHostedService<CouponExpiryEmailService>();
builder.Services
    .AddOptions<SuperAdminOptions>()
    .Bind(builder.Configuration.GetSection("SuperAdmin"))
    .Validate(
        options =>
        {
            var hasEmail = !string.IsNullOrWhiteSpace(options.Email);
            var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
            return (!hasEmail && !hasPassword)
                || (
                    hasEmail
                    && hasPassword
                    && new EmailAddressAttribute().IsValid(options.Email)
                    && options.Password!.Length >= 16
                );
        },
        "Configure both SuperAdmin:Email and a SuperAdmin:Password of at least 16 characters, or leave both empty to disable the admin account."
    )
    .ValidateOnStart();
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection("Email")
);
builder.Services.Configure<DropboxOptions>(
    builder.Configuration.GetSection("Dropbox")
);
builder.Services
    .AddOptions<DropboxPricingOptions>()
    .Bind(builder.Configuration.GetSection("DropboxPricing"))
    .Validate(
        options =>
            options.WeeklyPricePln > 0
            && !string.IsNullOrWhiteSpace(options.TestCouponCode)
            && options.TestDurationDays > 0,
        "Dropbox weekly price and test duration must be greater than zero, and the test coupon code must not be empty."
    )
    .ValidateOnStart();
builder.Services.AddHttpClient<IDropboxService, DropboxService>();
builder.Services.AddScoped<IPaymentService, MockPaymentService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured."
    );

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? "CoursePlatform";

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme
)
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType = "role",

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtIssuer,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        )
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var hasAuthorizationHeader =
                !string.IsNullOrWhiteSpace(
                    context.Request.Headers.Authorization
                );

            Console.WriteLine(
                $"JWT HEADER: {hasAuthorizationHeader}"
            );

            return Task.CompletedTask;
        },

        OnAuthenticationFailed = context =>
        {
            Console.WriteLine(
                $"JWT ERROR: {context.Exception.Message}"
            );

            return Task.CompletedTask;
        },

        OnTokenValidated = context =>
        {
            Console.WriteLine(
                "JWT OK - token został poprawnie zweryfikowany."
            );

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? new[]
    {
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:5175",
        "http://127.0.0.1:5173",
        "http://127.0.0.1:5174",
        "http://127.0.0.1:5175",
        "https://courseplatformfront.onrender.com"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (
    string.IsNullOrWhiteSpace(builder.Configuration["SuperAdmin:Email"])
    || string.IsNullOrWhiteSpace(builder.Configuration["SuperAdmin:Password"])
)
{
    app.Logger.LogWarning(
        "The superadmin account is disabled because SuperAdmin:Email and SuperAdmin:Password are not configured."
    );
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CourseDbContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    message = "CoursePlatform API działa",
    api = "/api/courses"
}));

app.Run();
