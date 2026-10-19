namespace Rcc.Api.Data;

public sealed class HotelService
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public string NameEs { get; set; } = "";
    public string NameEn { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class RoomType
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public string Slug { get; set; } = "";
    public string NameEs { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DescriptionEs { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string Category { get; set; } = "room";
    public string Beds { get; set; } = "king";
    public int Area { get; set; }
    public bool RiverView { get; set; }
    public int Capacity { get; set; }
    public int MaxAdults { get; set; }
    public int MaxChildren { get; set; }
    public decimal NightlyRate { get; set; }
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public long Revision { get; set; } = 1;
    public List<RoomImage> Images { get; set; } = [];
    public List<PhysicalRoom> Rooms { get; set; } = [];
}

public sealed class RoomImage
{
    public Guid Id { get; set; }
    public Guid RoomTypeId { get; set; }
    public string Url { get; set; } = "";
    public string AltEs { get; set; } = "";
    public string AltEn { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Credit { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class PhysicalRoom
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;
    public string Number { get; set; } = "";
    public bool Active { get; set; } = true;
    public long Revision { get; set; } = 1;
}
