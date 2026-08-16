using Microsoft.AspNetCore.Identity;
using NormalAssNote.Application.Authorization;

namespace NormalAssNote.Infrastructure.Identity;

internal static class IdentityRoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in IdentityRoleNames.All.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));

            // Another application instance may have created the role
            // concurrently. Recheck before considering it an error.
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
            {
                var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

                throw new InvalidOperationException($"Creating Identity role '{roleName}' failed. {errors}");
            }
        }
    }
}