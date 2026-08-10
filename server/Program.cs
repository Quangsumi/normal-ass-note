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

builder.Services.AddAntiforgery(options =>
{
    // Antiforgery verification work natively with form-bound data.
    // But the note sync endpoint accepts JSON, not form-bound data.
    // So need to custom header instead of the default form field to use antiforgery token.
    // logout endpoint is form-bound, so it can use the default form field.

    /*
     POST /api/notes/sync
    Cookie: __Host-normal-ass-note-v1=...
    Cookie: __Host-normal-ass-note-v1-antiforgery=... Why do we need this cookie? -> this cookie and the request token is a pair. 
                                                      Request token bind to this cookie. This Cookie prove the request token is valid and identify the browser session
    X-CSRF-TOKEN: REQUEST-TOKEN-HERE
    Content-Type: application/json
     */

    // React sends the request token using this header for JSON requests (ex: /notes/sync)
    options.HeaderName = "X-CSRF-TOKEN";

    // A real HTML form sending form-bound data will include request token in the form (ex: auth/logout/)
    options.FormFieldName = "__RequestVerificationToken";

    // React receives the request token from /api/auth/session, so it never
    // needs to read the antiforgery cookie.
    options.Cookie.Name = "__Host-normal-ass-note-v1-antiforgery-session";    

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
});

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

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
});

await app.RunAsync();
