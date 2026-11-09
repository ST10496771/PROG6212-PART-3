using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDayApi.Data;
using RaceDayApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));

// The "Testing" environment is used by the unit tests, which register their own in-memory
// database and in-memory image storage instead.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDB")));

    // ---------- Azure Blob Storage (event banners and profile pictures) ----------
    var storageConnection = builder.Configuration["AzureStorage:ConnectionString"];
    if (string.IsNullOrWhiteSpace(storageConnection))
        builder.Services.AddSingleton<IBlobStorageService, UnconfiguredBlobStorageService>();
    else
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDay.Session";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for managing events, categories, enrolments and results. Log in through /api/auth/login first, the session cookie is then used for all other requests."
    });
    options.EnableAnnotations();
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();

    // In Docker the SQL Server container needs a little while to start, so the API can retry
    // (switched on with Database__RetryOnStartup=true). In Visual Studio a failure is reported straight away.
    var retry = app.Configuration.GetValue("Database:RetryOnStartup", false);

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();

            if (app.Configuration.GetValue("SeedData", true))
                DbSeeder.Seed(db);

            break;
        }
        catch (Exception ex) when (retry && attempt < 45)
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/45). Trying again in 2 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

// Off by default so the MVC app can call the API over plain http (https redirection is switched on with UseHttpsRedirection=true).
if (app.Configuration.GetValue("UseHttpsRedirection", false) && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
