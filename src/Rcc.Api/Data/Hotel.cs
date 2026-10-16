namespace Rcc.Api.Data;

// Foundation entity only. Catalog, inventory and Identity belong to later modules.
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
}
