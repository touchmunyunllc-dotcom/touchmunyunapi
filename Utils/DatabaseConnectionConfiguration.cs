using Npgsql;

namespace ECommerce.Utils;

/// <summary>
/// Normalizes Render/Neon DATABASE_URL and connection strings for Npgsql.
/// </summary>
public static class DatabaseConnectionConfiguration
{
    public static void Apply(WebApplicationBuilder builder)
    {
        var fromConfig = builder.Configuration.GetConnectionString("DefaultConnection");
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        string? resolved = null;
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            resolved = ConvertDatabaseUrl(databaseUrl);
        }
        else if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            resolved = fromConfig.Trim();
            if (resolved.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                resolved = ConvertDatabaseUrl(resolved);
            }
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            return;
        }

        resolved = NormalizeForManagedPostgres(resolved);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = resolved,
        });
    }

    public static bool IsManagedPostgresHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Contains("neon.tech", StringComparison.OrdinalIgnoreCase)
            || host.Contains(".render.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("supabase.co", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForManagedPostgres(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Require,
            TrustServerCertificate = true,
            KeepAlive = 30,
            Timeout = 30,
            CommandTimeout = 60,
        };

        if (IsManagedPostgresHost(builder.Host))
        {
            // Neon pooler: avoid prepared statement issues on some pool modes
            builder.MaxPoolSize = Math.Max(builder.MaxPoolSize, 20);
        }

        return builder.ConnectionString;
    }

    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl.Trim());
        if (uri.Scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException(
                "DATABASE_URL must use postgres:// or postgresql:// scheme.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2 || string.IsNullOrEmpty(userInfo[0]))
        {
            throw new InvalidOperationException("DATABASE_URL is missing username or password.");
        }

        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = Uri.UnescapeDataString(userInfo[1]);
        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(database))
        {
            database = "postgres";
        }

        var port = uri.Port > 0 ? uri.Port : 5432;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            TrustServerCertificate = true,
        };

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
                var value = Uri.UnescapeDataString(kv[1]);
                if (key is "sslmode" or "ssl_mode" && value.Equals("require", StringComparison.OrdinalIgnoreCase))
                {
                    builder.SslMode = SslMode.Require;
                }
            }
        }

        return builder.ConnectionString;
    }
}
