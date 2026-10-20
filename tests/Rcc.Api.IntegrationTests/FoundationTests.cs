using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rcc.Api.Data;

namespace Rcc.Api.IntegrationTests;

public sealed class FoundationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Status_returns_current_module_contract()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/status");
        Assert.Equal("RCC", status.GetProperty("application").GetString());
        Assert.Equal(5, status.GetProperty("module").GetInt32());
    }

    [Fact]
    public async Task Readiness_requires_applied_migrations()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        await using var emptyFactory = postgres.CreateFactory(postgres.EmptyConnectionString);
        using var emptyClient = emptyFactory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await emptyClient.GetAsync("/api/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Database_outage_affects_readiness_but_not_liveness_and_hides_details()
    {
        var unavailable = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Host = "127.0.0.1", Port = 1, Timeout = 1, Pooling = false
        }.ConnectionString;
        await using var factory = postgres.CreateFactory(unavailable);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
        var response = await client.GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"Unhealthy\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_returns_404_problem_instead_of_spa()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/not-implemented");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Initial_migration_persists_bilingual_hotel_and_is_repeatable()
    {
        var id = Guid.NewGuid();
        await using (var database = postgres.OpenDbContext())
        {
            await database.Database.MigrateAsync();
            Assert.False(database.Database.HasPendingModelChanges());
            database.Hotels.Add(NewHotel(id));
            await database.SaveChangesAsync();
        }
        await using var reopened = postgres.OpenDbContext();
        var saved = await reopened.Hotels.SingleAsync(x => x.Id == id);
        Assert.Equal("Demostración en Chongqing", saved.DescriptionEs);
        Assert.Equal("Chongqing demonstration", saved.DescriptionEn);
        Assert.Equal(29.564700m, saved.Latitude);
    }

    [Fact]
    public async Task PostgreSql_rejects_invalid_coordinates_and_duplicate_slug()
    {
        await using (var database = postgres.OpenDbContext())
        {
            var invalid = NewHotel(Guid.NewGuid());
            invalid.Latitude = 91;
            database.Hotels.Add(invalid);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        await using (var database = postgres.OpenDbContext())
        {
            var original = NewHotel(Guid.NewGuid());
            database.Hotels.Add(original);
            await database.SaveChangesAsync();
            var duplicate = NewHotel(Guid.NewGuid());
            duplicate.Slug = original.Slug;
            database.Hotels.Add(duplicate);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
    }

    private static Hotel NewHotel(Guid id) => new()
    {
        Id = id, Slug = $"demo-{id:N}", Name = "RCC Demo",
        DescriptionEs = "Demostración en Chongqing", DescriptionEn = "Chongqing demonstration",
        Address = "Dirección ficticia de prueba", Latitude = 29.564700m, Longitude = 106.550700m
    };
}
