using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Rcc.Api.Catalog;
using Rcc.Api.Data;

namespace Rcc.Api.IntegrationTests;

public sealed class CatalogTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Rcc-Catalog-2026!Test";
    private static string Unique() => Guid.NewGuid().ToString("N");
    private static async Task<HttpClient> Admin(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"catalog-{Unique()}@example.test";
        var user = new IdentityUser { Email = email, UserName = email };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, "/auth/login", new { email, password = Password }, HttpMethod.Post)).StatusCode);
        return client;
    }
    private static async Task<HttpResponseMessage> Write(HttpClient client, string path, object body, HttpMethod? method = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Put, "/api" + path) { Content = JsonContent.Create(body) };
        if (csrf)
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        return await client.SendAsync(request);
    }
    private static RoomTypeInput NewType(string? slug = null) => new(
        slug ?? $"test-{Unique()}", "Suite de prueba", "Test suite", "Descripción de prueba", "Test description", "suite", "king", 80, true, 4, 3, 2, 1234.56m, true, 99, 0,
        [new ImageInput("/images/regent/classic.webp", "Dormitorio de prueba", "Test bedroom"), new ImageInput("/images/regent/classic-bathroom.webp", "Baño de prueba", "Test bathroom")]);
    private static async Task<RoomTypeView> CreateType(HttpClient client, RoomTypeInput input)
    {
        var response = await Write(client, "/admin/room-types", input, HttpMethod.Post);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoomTypeView>())!;
    }

    [Fact]
    public async Task Migration_seeds_bilingual_catalog_photos_and_demo_inventory_repeatably()
    {
        await using var database = postgres.OpenDbContext();
        await database.Database.MigrateAsync();
        Assert.False(database.Database.HasPendingModelChanges());
        var seedTypeIds = Enumerable.Range(1, 7).Select(index => Guid.Parse($"41000000-0000-0000-0000-{index:D12}")).ToArray();
        Assert.Equal(7, await database.RoomTypes.CountAsync(type => seedTypeIds.Contains(type.Id)));
        Assert.Equal(11, await database.RoomImages.CountAsync(image => seedTypeIds.Contains(image.RoomTypeId)));
        Assert.Equal(14, await database.Rooms.CountAsync(room => seedTypeIds.Contains(room.RoomTypeId)));
        var hotel = await database.Hotels.Include(x => x.Services).SingleAsync(x => x.Id == CatalogSeed.HotelId);
        Assert.NotEmpty(hotel.Services);
        Assert.Equal("Asia/Shanghai", hotel.TimeZone);
    }

    [Fact]
    public async Task Public_catalog_exposes_rates_capacity_credits_filters_and_bounded_pages()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/room-types?category=room&riverView=true&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        var room = page.GetProperty("items")[0];
        Assert.True(room.GetProperty("isDemo").GetBoolean());
        Assert.Equal("CNY", room.GetProperty("currency").GetString());
        Assert.True(room.GetProperty("nightlyRate").GetDecimal() > 0);
        Assert.Contains("ihg.com", room.GetProperty("images")[0].GetProperty("sourceUrl").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/room-types?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/room-types?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/room-types?category=invalid")).StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/room-types?search=not-found-" + Unique());
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/room-types/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admin_reads_and_writes_require_role_and_csrf()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/rooms")).StatusCode);
        var email = $"customer-{Unique()}@example.test";
        await Write(client, "/auth/register", new { email, password = Password }, HttpMethod.Post);
        await Write(client, "/auth/login", new { email, password = Password }, HttpMethod.Post);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/room-types")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Write(client, "/admin/room-types", NewType(), HttpMethod.Post)).StatusCode);
        using var admin = await Admin(factory);
        var payload = NewType();
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/room-types", payload, HttpMethod.Post, false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/hotel", new { }, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/rooms", new RoomInput(Guid.NewGuid(), "T-1", true, 0), HttpMethod.Post, false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/room-types/" + Guid.NewGuid(), payload, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/rooms/" + Guid.NewGuid(), new RoomInput(Guid.NewGuid(), "T-1", true, 0), csrf: false)).StatusCode);
    }

    [Fact]
    public async Task Create_update_deactivate_and_stale_edit_preserve_catalog()
    {
        await using var factory = postgres.CreateFactory();
        using var admin = await Admin(factory);
        using var visitor = factory.CreateClient();
        var input = NewType();
        var created = await CreateType(admin, input);
        Assert.Equal(1234.56m, created.NightlyRate);
        var changed = input with { Revision = created.Revision, NameEs = "Nombre editado", Active = false, Images = [input.Images![1], input.Images[0]] };
        var response = await Write(admin, "/admin/room-types/" + created.Id, changed);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<RoomTypeView>())!;
        Assert.Equal(created.Revision + 1, saved.Revision);
        Assert.EndsWith("classic-bathroom.webp", saved.Images[0].Url);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/room-types/" + created.Id)).StatusCode);
        var publicPage = await visitor.GetFromJsonAsync<JsonElement>("/api/room-types?search=" + input.Slug);
        Assert.Equal(0, publicPage.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, "/admin/room-types/" + created.Id, changed with { NameEs = "Sobrescritura" })).StatusCode);
        await using var database = postgres.OpenDbContext();
        Assert.Equal("Nombre editado", (await database.RoomTypes.SingleAsync(type => type.Id == created.Id)).NameEs);
        Assert.Equal(2, await database.RoomImages.CountAsync(image => image.RoomTypeId == created.Id));
        Assert.Equal(HttpStatusCode.OK, (await Write(admin, "/admin/room-types/" + created.Id, changed with { Revision = saved.Revision, Active = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/room-types/" + created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.DeleteAsync("/api/admin/room-types/" + created.Id)).StatusCode);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("precision")]
    [InlineData("capacity")]
    [InlineData("children")]
    [InlineData("adults")]
    [InlineData("asset")]
    [InlineData("translation")]
    [InlineData("images")]
    public async Task Server_rejects_invalid_catalog_fields_without_writes(string invalid)
    {
        await using var factory = postgres.CreateFactory();
        using var admin = await Admin(factory);
        var input = NewType();
        input = invalid switch
        {
            "price" => input with { NightlyRate = -1 },
            "precision" => input with { NightlyRate = 12.345m },
            "capacity" => input with { Capacity = 0 },
            "children" => input with { MaxChildren = input.Capacity },
            "adults" => input with { MaxAdults = 0 },
            "asset" => input with { Images = [new ImageInput("https://example.test/image.webp", "a", "b")] },
            "translation" => input with { NameEn = "" },
            "images" => input with { Images = [] },
            _ => input
        };
        var response = await Write(admin, "/admin/room-types", input, HttpMethod.Post);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var database = postgres.OpenDbContext();
        Assert.False(await database.RoomTypes.AnyAsync(type => type.Slug == input.Slug));
    }

    [Fact]
    public async Task Physical_inventory_has_unique_numbers_fixed_type_and_concurrency_control()
    {
        await using var factory = postgres.CreateFactory();
        using var admin = await Admin(factory);
        var type = await CreateType(admin, NewType());
        var number = "T-" + Unique()[..8];
        var input = new RoomInput(type.Id, number.ToLowerInvariant(), true, 0);
        var response = await Write(admin, "/admin/rooms", input, HttpMethod.Post);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal(number.ToUpperInvariant(), created.Number);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, "/admin/rooms", input with { Number = number }, HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/rooms", input with { RoomTypeId = Guid.NewGuid() }, HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, "/admin/rooms/" + created.Id, input with { Revision = created.Revision, RoomTypeId = Guid.NewGuid() })).StatusCode);
        var update = input with { Revision = created.Revision, Active = false };
        Assert.Equal(HttpStatusCode.OK, (await Write(admin, "/admin/rooms/" + created.Id, update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, "/admin/rooms/" + created.Id, update)).StatusCode);
        var publicType = await admin.GetFromJsonAsync<RoomTypeView>("/api/room-types/" + type.Id);
        Assert.Equal(0, publicType!.ActiveRooms);
        Assert.Equal(1, publicType.TotalRooms);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/rooms?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Hotel_updates_services_location_and_rejects_stale_version_and_invalid_coordinates()
    {
        await using var factory = postgres.CreateFactory();
        using var admin = await Admin(factory);
        var current = await admin.GetFromJsonAsync<HotelInput>("/api/hotel");
        var input = current! with { Name = "RCC Hotel editado", Services = [new ServiceInput("Servicio de prueba", "Test service")] };
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(admin, "/admin/hotel", input with { Latitude = 91 })).StatusCode);
        var response = await Write(admin, "/admin/hotel", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hotel = await admin.GetFromJsonAsync<JsonElement>("/api/hotel");
        Assert.Equal(input.Name, hotel.GetProperty("name").GetString());
        Assert.Single(hotel.GetProperty("services").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, "/admin/hotel", input)).StatusCode);
    }

    [Fact]
    public async Task Database_constraints_and_foreign_keys_protect_inventory()
    {
        var typeId = Guid.Parse("41000000-0000-0000-0000-000000000001");
        await using (var database = postgres.OpenDbContext())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"RoomTypes\" SET \"NightlyRate\" = -1 WHERE \"Id\" = {typeId}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        await using (var database = postgres.OpenDbContext())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"RoomTypes\" WHERE \"Id\" = {typeId}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
    }

    [Fact]
    public async Task Concurrent_type_creation_and_update_return_conflicts_without_partial_images()
    {
        await using var factory = postgres.CreateFactory();
        using var first = await Admin(factory);
        using var second = await Admin(factory);
        var input = NewType();
        var creates = await Task.WhenAll(Write(first, "/admin/room-types", input, HttpMethod.Post), Write(second, "/admin/room-types", input, HttpMethod.Post));
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Conflict);
        var type = (await creates.Single(response => response.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<RoomTypeView>())!;
        var update = input with { Revision = type.Revision, NightlyRate = 1450 };
        var responses = await Task.WhenAll(Write(first, "/admin/room-types/" + type.Id, update), Write(second, "/admin/room-types/" + type.Id, update with { NightlyRate = 1550 }));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var database = postgres.OpenDbContext();
        Assert.Equal(2, await database.RoomImages.CountAsync(image => image.RoomTypeId == type.Id));
    }
}
