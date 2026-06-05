namespace NormalAssNote.Api.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddClientCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigin = configuration["ClientOrigin"]
            ?? "http://localhost:5173;http://127.0.0.1:5173";

        var allowedOriginArray = allowedOrigin.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin => allowedOriginArray.Contains(origin))
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
