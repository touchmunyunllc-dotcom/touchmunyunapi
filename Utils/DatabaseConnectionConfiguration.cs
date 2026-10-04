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

        var resolved = ResolveConnectionString(databaseUrl, fromConfig);
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

    private static string? ResolveConnectionString(string? databaseUrl, string? fromConfig)
    {
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            var env = databaseUrl.Trim().Trim('"');
            if (IsPostgresUri(env))
            {
                return ConvertDatabaseUrl(env);
            }

            if (LooksLikeNpgsqlConnectionString(env))
            {
                return env;
            }

            Console.WriteLine(
                "WARNING: DATABASE_URL is not postgresql:// and not Npgsql key=value. " +
                "Unset DATABASE_URL or use ConnectionStrings__DefaultConnection.");
        }

        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            var config = fromConfig.Trim();
            if (IsPostgresUri(config))
            {
                return ConvertDatabaseUrl(config);
            }

            return config;
        }

        return null;
    }

    private static bool IsPostgresUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeNpgsqlConnectionString(string value) =>
        value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Server=", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeForManagedPostgres(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Require,
            KeepAlive = 30,
            Timeout = 30,
            CommandTimeout = 60,
        };

        if (IsManagedPostgresHost(builder.Host))
        {
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
