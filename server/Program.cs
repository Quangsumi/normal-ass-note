using NormalAssNote.Api.Endpoints;
using NormalAssNote.Api.Configuration;
using NormalAssNote.Application;
using NormalAssNote.Infrastructure;
using NormalAssNote.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddClientCors(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
var indexPath = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "index.html");

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapNoteEndpoints();

// app.MapFallback(async context =>
// {
//     if (context.Request.Path.StartsWithSegments("/api"))
//     {
//         context.Response.StatusCode = StatusCodes.Status404NotFound;
//         return;
//     }

//     context.Response.ContentType = "text/html; charset=utf-8";
//     await context.Response.SendFileAsync(indexPath);
// });

await app.RunAsync();
