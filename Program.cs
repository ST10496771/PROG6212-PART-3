using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDayApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDB")));
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    var blobConnectionString = builder.Configuration.GetConnectionString("AzureBlobStorage");
    if (string.IsNullOrWhiteSpace(blobConnectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:AzureBlobStorage is not set. Add it with user secrets or an environment variable.");
    }

    builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));
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
    var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsEnvironment("Testing"))
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
