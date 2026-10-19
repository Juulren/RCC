namespace Rcc.Api.Data;

public sealed class Hotel
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string DescriptionEs { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string TimeZone { get; set; } = "Asia/Shanghai";
    public long Revision { get; set; } = 1;
    public List<HotelService> Services { get; set; } = [];
}
