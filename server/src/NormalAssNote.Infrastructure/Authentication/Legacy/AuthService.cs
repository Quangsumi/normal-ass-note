using Microsoft.AspNetCore.Identity;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Infrastructure.Identity;

namespace NormalAssNote.Infrastructure.Authentication;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService tokenService) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRegister(request);
        if (validation is not null)
        {
            return validation;
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return AuthResult.Validation(result.Errors.ToDictionary(
                error => error.Code,
                error => new[] { error.Description }));
        }

        return AuthResult.Success(new AuthResponse(user.UserName, tokenService.Create(user)));
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateLogin(request);
        if (validation is not null)
        {
            return validation;
        }

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return AuthResult.UnauthorizedRequest();
        }

        return AuthResult.Success(new AuthResponse(user.UserName ?? request.UserName, tokenService.Create(user)));
    }

    private static AuthResult? ValidateLogin(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return AuthResult.Validation(new Dictionary<string, string[]>
            {
                ["userName"] = ["A username is required."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Validation(new Dictionary<string, string[]>
            {
                ["password"] = ["A password is required."]
            });
        }

        return null;
    }

    private static AuthResult? ValidateRegister(RegisterRequest request)
    {
        var validation = ValidateLogin(new LoginRequest(request.UserName, request.Password));
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return AuthResult.Validation(new Dictionary<string, string[]>
            {
                ["confirmPassword"] = ["Confirm password is required."]
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return AuthResult.Validation(new Dictionary<string, string[]>
            {
                ["confirmPassword"] = ["Passwords do not match."]
            });
        }

        return null;
    }
}
