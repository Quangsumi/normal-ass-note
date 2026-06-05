using NormalAssNote.Application.Authentication;

namespace NormalAssNote.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", RegisterAsync);
        app.MapPost("/api/auth/login", LoginAsync);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return ToHttpResult(result);
    }

    private static IResult ToHttpResult(AuthResult result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(result.Response);
        }

        if (result.Unauthorized)
        {
            return Results.Unauthorized();
        }

        return Results.ValidationProblem(result.Errors);
    }
}
