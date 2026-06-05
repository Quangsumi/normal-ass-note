using Microsoft.Extensions.DependencyInjection;
using NormalAssNote.Application.Notes;

namespace NormalAssNote.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<INoteService, NoteService>();
        return services;
    }
}
