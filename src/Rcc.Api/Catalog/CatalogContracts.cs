using System.Linq.Expressions;
using Rcc.Api.Data;

namespace Rcc.Api.Catalog;

public sealed record ServiceInput(string? NameEs, string? NameEn);
public sealed record HotelInput(string? Name, string? DescriptionEs, string? DescriptionEn, string? Address, decimal Latitude, decimal Longitude, string? TimeZone, long Revision, ServiceInput?[]? Services);
public sealed record ImageInput(string? Url, string? AltEs, string? AltEn);
public sealed record RoomTypeInput(string? Slug, string? NameEs, string? NameEn, string? DescriptionEs, string? DescriptionEn, string? Category, string? Beds, int Area, bool RiverView, int Capacity, int MaxAdults, int MaxChildren, decimal NightlyRate, bool Active, int SortOrder, long Revision, ImageInput?[]? Images);
public sealed record RoomInput(Guid RoomTypeId, string? Number, bool Active, long Revision);
public sealed record ImageView(Guid Id, string Url, string AltEs, string AltEn, int SortOrder, string SourceUrl, string Credit);
public sealed record RoomTypeView(Guid Id, string Slug, string NameEs, string NameEn, string DescriptionEs, string DescriptionEn, string Category, string Beds, int Area, bool RiverView, int Capacity, int MaxAdults, int MaxChildren, decimal NightlyRate, bool Active, int SortOrder, long Revision, int ActiveRooms, int TotalRooms, List<ImageView> Images, string Currency = "CNY", bool IsDemo = true);
public sealed record RoomView(Guid Id, Guid RoomTypeId, string Number, bool Active, long Revision, string TypeNameEs, string TypeNameEn, bool TypeActive);

public static class CatalogProjection
{
    public static readonly Expression<Func<RoomType, RoomTypeView>> Type = type => new RoomTypeView(
        type.Id, type.Slug, type.NameEs, type.NameEn, type.DescriptionEs, type.DescriptionEn, type.Category, type.Beds, type.Area,
        type.RiverView, type.Capacity, type.MaxAdults, type.MaxChildren, type.NightlyRate, type.Active, type.SortOrder, type.Revision,
        type.Rooms.Count(room => room.Active), type.Rooms.Count(),
        type.Images.OrderBy(image => image.SortOrder).Select(image => new ImageView(image.Id, image.Url, image.AltEs, image.AltEn, image.SortOrder, image.SourceUrl, image.Credit)).ToList(), "CNY", true);
    public static readonly Expression<Func<PhysicalRoom, RoomView>> Room = room => new RoomView(
        room.Id, room.RoomTypeId, room.Number, room.Active, room.Revision, room.RoomType.NameEs, room.RoomType.NameEn, room.RoomType.Active);
}
