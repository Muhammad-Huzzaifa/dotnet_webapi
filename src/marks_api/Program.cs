using marks_api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Database")
        )
        .UseSnakeCaseNamingConvention()
);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
