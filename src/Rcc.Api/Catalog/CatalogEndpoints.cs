using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;
using Rcc.Api.Reservations;

namespace Rcc.Api.Catalog;

public static class CatalogEndpoints
{
    public static void MapRccCatalog(this WebApplication app)
    {
        var catalog = app.MapGroup("/api").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        catalog.MapGet("/hotel", GetHotel);
        catalog.MapGet("/room-types", (RccDbContext db, int page = 1, int pageSize = 20, string? search = null, string? category = null, bool? riverView = null) => ListTypes(db, false, page, pageSize, search, category, null, riverView));
        catalog.MapGet("/room-types/{id:guid}", async (Guid id, RccDbContext db) =>
        {
            var item = await db.RoomTypes.AsNoTracking().Where(x => x.HotelId == CatalogSeed.HotelId && x.Id == id && x.Active).Select(CatalogProjection.Type).SingleOrDefaultAsync();
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        var admin = catalog.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole("Admin")).AddEndpointFilter<CatalogWriteFilter>();
        admin.MapGet("/hotel", GetHotel);
        admin.MapPut("/hotel", UpdateHotel);
        admin.MapGet("/image-assets", () => Results.Ok(CatalogAssets.Names.Select(name => new { url = $"/images/regent/{name}.webp", sourceUrl = CatalogAssets.Source, credit = CatalogAssets.Credit })));
        admin.MapGet("/room-types", (RccDbContext db, int page = 1, int pageSize = 20, string? search = null, string? category = null, bool? active = null) => ListTypes(db, true, page, pageSize, search, category, active, null));
        admin.MapPost("/room-types", CreateType);
        admin.MapPut("/room-types/{id:guid}", UpdateType);
        admin.MapGet("/rooms", ListRooms);
        admin.MapPost("/rooms", CreateRoom);
        admin.MapPut("/rooms/{id:guid}", UpdateRoom);
    }

    private static async Task<IResult> GetHotel(RccDbContext db)
    {
        var hotel = await db.Hotels.AsNoTracking().Where(x => x.Id == CatalogSeed.HotelId).Select(x => new
        {
            x.Id,
            x.Name,
            x.DescriptionEs,
            x.DescriptionEn,
            x.Address,
            x.Latitude,
            x.Longitude,
            x.TimeZone,
            x.Revision,
            services = x.Services.OrderBy(service => service.SortOrder).Select(service => new { service.NameEs, service.NameEn }).ToList(),
            currency = "CNY",
            isDemo = true,
            coordinatesApproximate = true
        }).SingleOrDefaultAsync();
        return hotel is null ? Results.NotFound() : Results.Ok(hotel);
    }
    private static async Task<IResult> ListTypes(RccDbContext db, bool admin, int page, int pageSize, string? search, string? category, bool? active, bool? riverView)
    {
        if (!ValidPage(page, pageSize, search) || category is not (null or "room" or "suite")) return Error(400, "INVALID_FILTER");
        var query = db.RoomTypes.AsNoTracking().Where(x => x.HotelId == CatalogSeed.HotelId);
        if (!admin) query = query.Where(x => x.Active);
        else if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        if (category is not null) query = query.Where(x => x.Category == category);
        if (riverView.HasValue) query = query.Where(x => x.RiverView == riverView);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.NameEs.ToLower().Contains(text) || x.NameEn.ToLower().Contains(text) || x.Slug.Contains(text));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(CatalogProjection.Type).ToListAsync();
        return Results.Ok(new { items, page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize), currency = "CNY", isDemo = true });
    }
    private static async Task<IResult> UpdateHotel(HotelInput input, RccDbContext db)
    {
        var errors = CatalogValidation.Hotel(input);
        if (errors.Count > 0) return Invalid(errors);
        var hotel = await db.Hotels.Include(x => x.Services).SingleOrDefaultAsync(x => x.Id == CatalogSeed.HotelId);
        if (hotel is null) return Results.NotFound();
        if (input.Revision != hotel.Revision) return Error(409, "CATALOG_CHANGED");
        await using var transaction = await db.Database.BeginTransactionAsync();
        hotel.Name = CatalogValidation.Clean(input.Name);
        hotel.DescriptionEs = CatalogValidation.Clean(input.DescriptionEs);
        hotel.DescriptionEn = CatalogValidation.Clean(input.DescriptionEn);
        hotel.Address = CatalogValidation.Clean(input.Address);
        hotel.Latitude = input.Latitude; hotel.Longitude = input.Longitude;
        hotel.Revision++;
        db.RemoveRange(hotel.Services);
        hotel.Services = input.Services!.Select((service, index) => new HotelService { Id = Guid.NewGuid(), HotelId = hotel.Id, NameEs = CatalogValidation.Clean(service!.NameEs), NameEn = CatalogValidation.Clean(service.NameEn), SortOrder = index }).ToList();
        db.AddRange(hotel.Services);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetHotel(db);
    }
    private static async Task<IResult> CreateType(RoomTypeInput input, RccDbContext db)
    {
        var errors = CatalogValidation.Type(input);
        if (errors.Count > 0) return Invalid(errors);
        var type = new RoomType { Id = Guid.NewGuid(), HotelId = CatalogSeed.HotelId };
        Apply(type, input);
        type.Images = Images(type.Id, input.Images!);
        db.RoomTypes.Add(type);
        await db.SaveChangesAsync();
        return Results.Created($"/api/room-types/{type.Id}", await db.RoomTypes.Where(x => x.Id == type.Id).Select(CatalogProjection.Type).SingleAsync());
    }
    private static async Task<IResult> UpdateType(Guid id, RoomTypeInput input, RccDbContext db, TimeProvider clock)
    {
        var errors = CatalogValidation.Type(input);
        if (errors.Count > 0) return Invalid(errors);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        var type = await db.RoomTypes.Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id && x.HotelId == CatalogSeed.HotelId);
        if (type is null) return Results.NotFound();
        if (type.Revision != input.Revision) return Error(409, "CATALOG_CHANGED");
        if (!input.Active && await Inventory.HasFutureAsync(db, null, id, clock)) return Error(409, "INVENTORY_COMMITTED");
        Apply(type, input); type.Revision++;
        db.RoomImages.RemoveRange(type.Images);
        await db.SaveChangesAsync();
        type.Images = Images(type.Id, input.Images!);
        db.RoomImages.AddRange(type.Images);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(await db.RoomTypes.AsNoTracking().Where(x => x.Id == id).Select(CatalogProjection.Type).SingleAsync());
    }
    private static void Apply(RoomType type, RoomTypeInput input)
    {
        type.Slug = CatalogValidation.Clean(input.Slug); type.NameEs = CatalogValidation.Clean(input.NameEs); type.NameEn = CatalogValidation.Clean(input.NameEn);
        type.DescriptionEs = CatalogValidation.Clean(input.DescriptionEs); type.DescriptionEn = CatalogValidation.Clean(input.DescriptionEn);
        type.Category = input.Category!; type.Beds = input.Beds!; type.Area = input.Area; type.RiverView = input.RiverView;
        type.Capacity = input.Capacity; type.MaxAdults = input.MaxAdults; type.MaxChildren = input.MaxChildren;
        type.NightlyRate = input.NightlyRate; type.Active = input.Active; type.SortOrder = input.SortOrder;
    }
    private static List<RoomImage> Images(Guid typeId, ImageInput?[] images) => images.Select((image, index) => new RoomImage
    {
        Id = Guid.NewGuid(),
        RoomTypeId = typeId,
        Url = image!.Url!,
        AltEs = CatalogValidation.Clean(image.AltEs),
        AltEn = CatalogValidation.Clean(image.AltEn),
        SortOrder = index,
        SourceUrl = CatalogAssets.Source,
        Credit = CatalogAssets.Credit
    }).ToList();
    private static async Task<IResult> ListRooms(RccDbContext db, int page = 1, int pageSize = 20, string? search = null, Guid? roomTypeId = null, bool? active = null)
    {
        if (!ValidPage(page, pageSize, search)) return Error(400, "INVALID_FILTER");
        var query = db.Rooms.AsNoTracking().Where(x => x.HotelId == CatalogSeed.HotelId);
        if (roomTypeId.HasValue) query = query.Where(x => x.RoomTypeId == roomTypeId);
        if (active.HasValue) query = query.Where(x => x.Active == active);
        if (!string.IsNullOrWhiteSpace(search)) { var text = search.Trim().ToUpperInvariant(); query = query.Where(x => x.Number.Contains(text)); }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Number).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(CatalogProjection.Room).ToListAsync();
        return Results.Ok(new { items, page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) });
    }
    private static async Task<IResult> CreateRoom(RoomInput input, RccDbContext db)
    {
        var errors = CatalogValidation.Room(input);
        if (errors.Count > 0) return Invalid(errors);
        if (!await db.RoomTypes.AnyAsync(x => x.Id == input.RoomTypeId && x.HotelId == CatalogSeed.HotelId)) return Error(400, "INVALID_ROOM_TYPE");
        var room = new PhysicalRoom { Id = Guid.NewGuid(), HotelId = CatalogSeed.HotelId, RoomTypeId = input.RoomTypeId, Number = CatalogValidation.Clean(input.Number).ToUpperInvariant(), Active = input.Active };
        db.Rooms.Add(room); await db.SaveChangesAsync();
        return Results.Created($"/api/admin/rooms/{room.Id}", await db.Rooms.Where(x => x.Id == room.Id).Select(CatalogProjection.Room).SingleAsync());
    }
    private static async Task<IResult> UpdateRoom(Guid id, RoomInput input, RccDbContext db, TimeProvider clock)
    {
        var errors = CatalogValidation.Room(input);
        if (errors.Count > 0) return Invalid(errors);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == id && x.HotelId == CatalogSeed.HotelId);
        if (room is null) return Results.NotFound();
        if (room.Revision != input.Revision) return Error(409, "CATALOG_CHANGED");
        if (room.RoomTypeId != input.RoomTypeId) return Error(409, "ROOM_TYPE_IMMUTABLE");
        if ((!input.Active || room.Number != CatalogValidation.Clean(input.Number).ToUpperInvariant()) && await Inventory.HasFutureAsync(db, id, null, clock)) return Error(409, "INVENTORY_COMMITTED");
        room.Number = CatalogValidation.Clean(input.Number).ToUpperInvariant(); room.Active = input.Active; room.Revision++;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(await db.Rooms.AsNoTracking().Where(x => x.Id == id).Select(CatalogProjection.Room).SingleAsync());
    }
    private static bool ValidPage(int page, int pageSize, string? search) => page is >= 1 and <= 1000000 && pageSize is >= 1 and <= 100 && (search?.Length ?? 0) <= 100;
    private static IResult Invalid(Dictionary<string, string[]> errors) => Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });
    public static IResult Error(int status, string code) => Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
}
