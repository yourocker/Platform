using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Core.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = BuildConfiguration(environment);
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Не удалось получить строку подключения DefaultConnection для design-time AppDbContext.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure());

        return new AppDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration(string environment)
    {
        foreach (var basePath in GetCandidateBasePaths())
        {
            var appSettingsPath = Path.Combine(basePath, "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                continue;
            }

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        throw new InvalidOperationException("Не найден appsettings.json для design-time AppDbContext.");
    }

    private static IEnumerable<string> GetCandidateBasePaths()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        yield return currentDirectory;
        yield return Path.Combine(currentDirectory, "CRM");
        yield return Path.GetFullPath(Path.Combine(currentDirectory, "..", "CRM"));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CRM"));
    }
}
