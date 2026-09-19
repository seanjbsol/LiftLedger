using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiftLedger.Api.Tests;

public class TestAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", JwtTestKeys.SigningKey);
        builder.UseSetting("Jwt:Issuer", "LiftLedger");
        builder.UseSetting("Jwt:Audience", "LiftLedger.Clients");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Database:Provider", "Sqlite");

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(AppDbContext)
                    || d.ImplementationType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}

internal static class JwtTestKeys
{
    public const string SigningKey = "TEST-ONLY-SIGNING-KEY-LiftLedger-32chars!";
}

public static class HttpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        return JsonSerializer.Deserialize<T>(body, Options)
               ?? throw new InvalidOperationException("Empty response body.");
    }
}

public static class AuthHelpers
{
    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password = DatabaseSeeder.DemoPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        return await response.ReadAsync<AuthResponse>();
    }

    public static HttpClient As(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
