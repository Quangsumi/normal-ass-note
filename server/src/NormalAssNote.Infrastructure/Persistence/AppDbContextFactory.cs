using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NormalAssNote.Infrastructure.Persistence;

// This class is used to create an instance of the AppDbContext at design time,
// which is necessary for certain Entity Framework Core commands, such as migrations.
// It implements the IDesignTimeDbContextFactory interface, which requires the implementation of the CreateDbContext method.
// This method reads the configuration from appsettings.json and environment variables to construct the connection string and
// create a new instance of AppDbContext with the appropriate options.
//
// Design-time is when you are working with the database schema, such as creating or applying migrations, rather than running the application itself.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindServerDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static string FindServerDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var serverDirectory = Path.Combine(directory.FullName, "server");
            if (File.Exists(Path.Combine(serverDirectory, "appsettings.json")))
            {
                return serverDirectory;
            }

            if (File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
