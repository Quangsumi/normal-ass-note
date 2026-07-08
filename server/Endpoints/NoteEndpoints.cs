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
        notes.MapGet("/{noteId}", GetAsync);
        notes.MapPost("/sync", SyncAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        INoteService notes,
        string? contentNoteId,
        CancellationToken cancellationToken)
    {
        var savedNotes = await notes.ListAsync(
            principal.UserId(),
            contentNoteId,
            cancellationToken);
        return Results.Ok(savedNotes);
    }

    private static async Task<IResult> GetAsync(
        string noteId,
        ClaimsPrincipal principal,
        INoteService notes,
        CancellationToken cancellationToken)
    {
        var savedNote = await notes.GetAsync(principal.UserId(), noteId, cancellationToken);
        return savedNote is null ? Results.NotFound() : Results.Ok(savedNote);
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
