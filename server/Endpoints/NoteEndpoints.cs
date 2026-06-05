using System.Security.Claims;
using NormalAssNote.Api.Auth;
using NormalAssNote.Application.Notes;

namespace NormalAssNote.Api.Endpoints;

public static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/notes").RequireAuthorization();

        notes.MapGet("", ListAsync);
        notes.MapPost("/sync", SyncAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        INoteService notes,
        CancellationToken cancellationToken)
    {
        var savedNotes = await notes.ListAsync(principal.UserId(), cancellationToken);
        return Results.Ok(savedNotes);
    }

    private static async Task<IResult> SyncAsync(
        NoteSyncRequest request,
        ClaimsPrincipal principal,
        INoteService notes,
        CancellationToken cancellationToken)
    {
        var savedNotes = await notes.SyncAsync(principal.UserId(), request, cancellationToken);
        return Results.Ok(savedNotes);
    }
}
