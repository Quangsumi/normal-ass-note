using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Application.Common;
using NormalAssNote.Application.Notes;
using NormalAssNote.Infrastructure.Authentication;
using NormalAssNote.Infrastructure.Common;
using NormalAssNote.Infrastructure.Identity;
using NormalAssNote.Infrastructure.Notes;
using NormalAssNote.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NormalAssNote.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        #region Data Protection Certificate, used to encrypt/decrypt Data Protection Key Rings
        //var certificatePath = configuration["DataProtection:CertificatePath"] 
        //    ?? throw new InvalidOperationException("DataProtection:CertificatePath is required.");

        //var certificatePasswordFile = configuration["DataProtection:CertificatePasswordFile"]
        //    ?? throw new InvalidOperationException("DataProtection:CertificatePasswordFile is required.");

        //if (!File.Exists(certificatePath))
        //{
        //    throw new InvalidOperationException($"Data Protection certificate was not found at '{certificatePath}'.");
        //}

        //if (!File.Exists(certificatePasswordFile))
        //{
        //    throw new InvalidOperationException($"Data Protection certificate password file was not found at " + $"'{certificatePasswordFile}'.");
        //}

        //var certificatePassword = File.ReadAllText(certificatePasswordFile).TrimEnd('\r', '\n');

        //var dataProtectionCertificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);

        //if (!dataProtectionCertificate.HasPrivateKey)
        //{
        //    throw new InvalidOperationException("The Data Protection certificate does not contain a private key.");
        //}
        #endregion

        services.AddDataProtection()
            .SetApplicationName("normal-ass-note-v1")  // Must be identical across all replicas.
                                                       // It participates in cryptographic isolation in cookies, antiforgery, database tickets, ...
                                                       // Changing it invalidates all existing cookies and antiforgery tokens.
            .PersistKeysToDbContext<AppDbContext>();
            //.ProtectKeysWithCertificate(dataProtectionCertificate);  // TODO: Wrap/encrypt data protection key ring using this X.509 certificate.

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<PostgresTicketStore>(); // TODO: move to RedisTicketStore for better performance or use Postgres Caching feature
        services.AddHostedService<AuthenticationSessionCleanupService>();

        services.AddScoped<IOidcLogoutTokenValidator, OidcLogoutTokenValidator>();
        services.AddScoped<IOidcSessionRevoker, OidcSessionRevoker>();

        services.AddAuthenticationServices(configuration);
        
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<INoteRepository, NoteRepository>();

        return services;
    }

    private static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Temporary legacy services. Remove after the JWT migration is complete.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<OidcUserProvisioner>();

        var jwtOptions =
            configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "Jwt settings are not configured.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
            || string.IsNullOrWhiteSpace(jwtOptions.Audience)
            || string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience, and Jwt:SigningKey are required.");
        }

        var oidcOptions =
            configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>()
            ?? throw new InvalidOperationException(
                "Oidc settings are not configured.");

        if (string.IsNullOrWhiteSpace(oidcOptions.Authority)
            || string.IsNullOrWhiteSpace(oidcOptions.ClientId)
            || string.IsNullOrWhiteSpace(oidcOptions.ClientSecret)
            || oidcOptions.AllowedLogoutTokenAlgorithms is not { Length: > 0 }
            || oidcOptions.AllowedLogoutTokenAlgorithms.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Oidc:Authority, Oidc:ClientId, " + "Oidc:ClientSecret, and at least one " 
                + "Oidc:AllowedLogoutTokenAlgorithms value " + "are required.");
        }

        if (!Uri.TryCreate(
                oidcOptions.Authority,
                UriKind.Absolute,
                out var authorityUri)
            || authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Oidc:Authority must be an absolute HTTPS issuer URL.");
        }

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services.AddSingleton(jwtOptions);
        services.AddSingleton(oidcOptions);

        const string KeycloakOidc = "keycloak";
        const string AppCookie = "note-cookie";
        const string LegacyJwt = "legacy-jwt";

        services
            .AddAuthentication(options =>
            {
                // Used to authenticate every normal request.
                options.DefaultAuthenticateScheme = AppCookie;

                // Where a successful OIDC login creates the local session.
                options.DefaultSignInScheme = AppCookie;

                // API authorization failure should be 401/403. Only /api/auth/login explicitly challenges OIDC.
                // Failed API authorization uses the cookie handler, not an automatic redirect to Keycloak.
                options.DefaultChallengeScheme = AppCookie;
                options.DefaultForbidScheme = AppCookie;
            })
            .AddCookie(AppCookie, options =>
            {
                options.Cookie.Name = "__Host-normal-ass-note-v1-session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;

                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;

                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(KeycloakOidc, options =>
            {
                options.SignInScheme = AppCookie;

                options.Authority = oidcOptions.Authority;
                options.ClientId = oidcOptions.ClientId;
                options.ClientSecret = oidcOptions.ClientSecret;

                options.RequireHttpsMetadata = true;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                options.MapInboundClaims = false;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = false;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";

                /*
                 * Disable ASP.NET's cookie-dependent RemoteSignOutPath.
                 * we use endpoint to revokes the PostgreSQL session using (iss, sid).
                 */
                options.RemoteSignOutPath = PathString.Empty;
                // options.RemoteSignOutPath = "/oidc/frontchannel-logout";

                options.TokenValidationParameters.NameClaimType = "preferred_username";

                /*
                     ID token signature/issuer validation
                        ↓
                    OnTokenValidated
                        ↓
                    Read context.SecurityToken.Issuer
                        ↓
                    Add trusted iss to context.Principal
                        ↓
                    Further OIDC checks complete
                        ↓
                    OnTicketReceived
                        ↓
                    Cookie is issued containing iss
                 */
                const string ValidatedIssuerProperty = ".normal-ass-note.validated-issuer";

                options.Events.OnTokenValidated = context =>
                {
                    var validatedIssuer = context.SecurityToken.Issuer;

                    if (string.IsNullOrWhiteSpace(validatedIssuer))
                    {
                        context.Fail("The validated token has no issuer.");
                        return Task.CompletedTask;
                    }

                    context.Properties?.Items[ValidatedIssuerProperty] = validatedIssuer;

                    return Task.CompletedTask;
                };

                /*
                    Keycloak authen user successfully then redirects to asp.net /signin-oidc
                        ↓
                    ASP.NET validates state, code, tokens, issuer, signature, nonce
                        ↓
                    ASP.NET creates an AuthenticationTicket
                        ↓
                    OnTicketReceived runs
                        ↓
                    ASP.NET sends that ticket to the cookie authentication handler
                        ↓
                    Cookie is created
                 */
                options.Events.OnTicketReceived = async context =>
                {
                    // context.Principal and ClaimsPrincipal object is used for cookie creation.
                    // once the cookie is created, this object is no longer used.
                    // OnTicketReceived only runs once per successful OIDC login, not on every request.
                    // Subsequent requests will use the cookie and not this object and not go through OIDC again until the cookie expires.

                    if (context.Principal?.Identity is not ClaimsIdentity identity)
                    {
                        context.Fail("OIDC authentication did not produce a claims identity.");
                        return;
                    }

                    if (!context.Properties.Items.TryGetValue(ValidatedIssuerProperty, out var validatedIssuer) || string.IsNullOrWhiteSpace(validatedIssuer))
                    {
                        context.Fail("The validated OIDC issuer was not preserved.");
                        return;
                    }

                    // Don't persist the temporary property separately.
                    context.Properties!.Items.Remove(ValidatedIssuerProperty);

                    foreach (var claim in identity.FindAll("iss").ToArray())
                    {
                        identity.RemoveClaim(claim);
                    }

                    identity.AddClaim(new Claim(
                        "iss",
                        validatedIssuer!,
                        ClaimValueTypes.String,
                        context.Scheme.Name));

                    try
                    {
                        var provisioner = context.HttpContext.RequestServices.GetRequiredService<OidcUserProvisioner>();

                        var applicationUser = await provisioner.ResolveAsync(context.Principal);

                        // upstream may issue a claim with the same name as app_user_id, remove it to be safe
                        foreach (var existingClaim in identity.FindAll(AppClaimTypes.UserId).ToArray())
                        {
                            identity.RemoveClaim(existingClaim);
                        }

                        identity.AddClaim(new Claim(
                            AppClaimTypes.UserId,
                            applicationUser.Id,
                            ClaimValueTypes.String,
                            issuer: "normal-ass-note"));
                    }
                    catch (Exception exception)
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NormalAssNote.OidcUserMapping");

                        logger.LogError(exception, "OIDC authentication succeeded, but local user mapping failed.");

                        context.Fail("The OIDC identity could not be linked to a local note account.");
                    }
                };
            })
            .AddJwtBearer(LegacyJwt, options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = signingKey,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
            });
        
        services.AddOptions<CookieAuthenticationOptions>(AppCookie)
            .Configure<PostgresTicketStore>((options, ticketStore) =>
            {
                options.SessionStore = ticketStore;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("BrowserCookie", policy =>
            {
                policy.AddAuthenticationSchemes(AppCookie);
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy("LegacyBearer", policy =>
            {
                policy.AddAuthenticationSchemes(LegacyJwt);
                policy.RequireAuthenticatedUser();
            });
        });

        return services;
    }
}
