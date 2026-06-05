using NormalAssNote.Infrastructure.Identity;

namespace NormalAssNote.Infrastructure.Authentication;

internal interface IJwtTokenService
{
    string Create(ApplicationUser user);
}
