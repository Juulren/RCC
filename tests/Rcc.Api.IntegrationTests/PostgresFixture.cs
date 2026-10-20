using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rcc.Api.Data;

namespace Rcc.Api.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly List<string> databases = [];
    private string adminConnection = "";
    public string ConnectionString { get; private set; } = "";
    public string EmptyConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("RCC_TEST_POSTGRES")
            ?? throw new InvalidOperationException(
                "Configure RCC_TEST_POSTGRES con una instancia PostgreSQL de pruebas y permiso CREATEDB. No se omiten pruebas.");
        ConnectionString = await CreateDatabaseAsync();
        EmptyConnectionString = await CreateDatabaseAsync();
        await using var database = OpenDbContext();
        await database.Database.MigrateAsync();
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"rcc_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
        await command.ExecuteNonQueryAsync();
        databases.Add(name);
        return new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
    }

    public RccDbContext OpenDbContext() => new(
        new DbContextOptionsBuilder<RccDbContext>().UseNpgsql(ConnectionString).Options);

    public WebApplicationFactory<Program> CreateFactory(string? connection = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions.PostConfigure<Rcc.Api.Services.EmailSettings>(services, options => options.Enabled = false));
            builder.UseSetting("ConnectionStrings:Rcc", connection ?? ConnectionString);
        });

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        foreach (var name in databases)
        {
            // Only databases named and created by this fixture can be dropped.
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^rcc_test_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Unsafe test database name.");
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}
