using System.Security.Claims;
using System.Text;
using marks_api.Data;
using marks_api.Data.Entities;
using marks_api.Dtos.Auth;
using marks_api.Endpoints;
using marks_api.Extensions;
using marks_api.Services.Implementations;
using marks_api.Services.Interfaces;
using marks_api.SmokeTests;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Smoke tests only run when explicitly requested. They never run during a
// normal application start.
var smokeTestRequested =
    args.Any(arg => string.Equals(
        arg,
        "--smoke-test",
        StringComparison.OrdinalIgnoreCase)) ||
    string.Equals(
        Environment.GetEnvironmentVariable("SMOKE_TEST"),
        "true",
        StringComparison.OrdinalIgnoreCase) ||
    builder.Configuration.GetValue<bool>("SmokeTest:Enabled");

// PostgreSQL database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Database")
        )
        .UseSnakeCaseNamingConvention()
);

// JWT configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

var jwtSecretKey = jwtSettings["SecretKey"];

if (string.IsNullOrWhiteSpace(jwtSecretKey) ||
    Encoding.UTF8.GetByteCount(jwtSecretKey) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is missing or shorter than 32 bytes. " +
        "Configure it with User Secrets."
    );
}

var jwtIssuer = jwtSettings["Issuer"] ?? "marks_api";
var jwtAudience = jwtSettings["Audience"] ?? "marks_api_clients";

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<PasswordHasher<User>>();

// Authentication and authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecretKey)
            ),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role",
            NameClaimType = "unique_name"
        };

        // Keep 401 and 403 responses consistent with the rest of the API.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.Headers["WWW-Authenticate"] = "Bearer";

                await Results.Problem(
                    ProblemDetailsExtensions.CreateProblem(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "Unauthorized",
                        "A valid bearer token is required."))
                    .ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                await Results.Problem(
                    ProblemDetailsExtensions.CreateProblem(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        "You do not have permission to access this resource."))
                    .ExecuteAsync(context.HttpContext);
            }
        };
    });

builder.Services.AddAuthorization();

// Standardized error responses and a single global exception handler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Restricted CORS: only origins configured in appsettings are allowed.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Health checks: the application process and PostgreSQL connectivity.
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // AppDbContext is scoped, so resolve it inside a dedicated scope during startup.
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await DbSeeder.SeedAsync(context);
}

app.UseExceptionHandler();

// Unmatched routes and framework-produced status codes also return
// ProblemDetails instead of an empty body.
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;

    if (response.HasStarted ||
        response.ContentLength is > 0 ||
        response.StatusCode < 400)
    {
        return;
    }

    await Results.Problem(
        ProblemDetailsExtensions.CreateProblem(
            statusCodeContext.HttpContext,
            response.StatusCode,
            ReasonPhrases.GetReasonPhrase(response.StatusCode),
            "The request could not be completed."))
        .ExecuteAsync(statusCodeContext.HttpContext);
});

app.UseHttpsRedirection();

app.UseCors("Default");

// Authentication must run before authorization.
app.UseAuthentication();
app.UseAuthorization();

// Login: accepts credentials and returns a JWT access token.
app.MapPost("/api/auth/login", async (
    LoginRequest request,
    IAuthService authService,
    CancellationToken cancellationToken) =>
{
    var isValid = request.TryValidate(out var validationErrors);

    // [Required] does not reject whitespace-only values.
    if (string.IsNullOrWhiteSpace(request.Username))
    {
        validationErrors["username"] = ["Username is required."];
        isValid = false;
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        validationErrors["password"] = ["Password is required."];
        isValid = false;
    }

    if (!isValid)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var response = await authService.LoginAsync(
        request,
        cancellationToken);

    return response is null
        ? Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Invalid username or password.")
        : Results.Ok(response);
})
.AllowAnonymous()
.WithName("Login");

// Returns the identity encoded in the current access token.
app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        userId = user.FindFirstValue("sub"),
        username = user.FindFirstValue("unique_name"),
        role = user.FindFirstValue("role"),
        studentId = user.FindFirstValue("studentId")
    });
})
.RequireAuthorization()
.WithName("GetCurrentUser");

app.MapStudentEndpoints();
app.MapCourseEndpoints();
app.MapEnrollmentEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs =
                (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs =
                    (long)entry.Value.Duration.TotalMilliseconds,
                description =
                    entry.Value.Status == HealthStatus.Healthy
                        ? "Reachable."
                        : "Health check failed."
            })
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.MapGet("/", () => "Hello World!");

if (smokeTestRequested)
{
    return await SmokeTestRunner.RunAsync(app, builder.Configuration);
}

await app.RunAsync();

return 0;
