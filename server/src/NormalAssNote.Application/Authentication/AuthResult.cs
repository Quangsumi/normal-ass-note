namespace NormalAssNote.Application.Authentication;

public sealed record AuthResult(
    AuthResponse? Response,
    IReadOnlyDictionary<string, string[]> Errors,
    bool Unauthorized)
{
    public bool Succeeded => Response is not null;

    public static AuthResult Success(AuthResponse response) =>
        new(response, new Dictionary<string, string[]>(), false);

    public static AuthResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, errors, false);

    public static AuthResult UnauthorizedRequest() =>
        new(null, new Dictionary<string, string[]>(), true);
}
