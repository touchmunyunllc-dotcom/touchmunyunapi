using System.Data;
using System.Text.Json;
using Dapper;
using ECommerce.Models;

namespace ECommerce.Services;

public class CountryService : ICountryService
{
    private readonly IDbConnection _connection;

    public CountryService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<Country>> GetAllAsync()
    {
        var rows = await _connection.QueryAsync<Country>(
            """
            SELECT code AS Code, name AS Name
            FROM countries
            ORDER BY CASE WHEN code = 'US' THEN 0 ELSE 1 END, name
            """);
        return rows.ToList();
    }

    public async Task<bool> ExistsAsync(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length != 2)
        {
            return false;
        }

        var found = await _connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT 1 FROM countries WHERE code = @Code",
            new { Code = code });
        return found == 1;
    }

    public async Task SeedCountriesIfEmptyAsync()
    {
        var count = await _connection.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM countries");
        if (count > 0)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "countries.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("countries.json seed file not found.", path);
        }

        var json = await File.ReadAllTextAsync(path);
        var entries = JsonSerializer.Deserialize<List<CountrySeedEntry>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            await _connection.ExecuteAsync(
                """
                INSERT INTO countries (code, name)
                VALUES (@Code, @Name)
                ON CONFLICT (code) DO NOTHING
                """,
                new { Code = entry.Code.Trim().ToUpperInvariant(), Name = entry.Name.Trim() });
        }
    }

    private sealed class CountrySeedEntry
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
