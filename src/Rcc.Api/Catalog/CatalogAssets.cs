namespace Rcc.Api.Catalog;

public static class CatalogAssets
{
    public const string Source = "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites";
    public const string Credit = "Regent / IHG · Copyright de sus titulares";
    public static readonly string[] Names = ["classic", "classic-bathroom", "classic-river", "club", "club-river", "junior", "junior-living", "executive", "executive-living", "regent", "regent-living"];
    public static bool IsApproved(string? url) => Names.Any(name => url == $"/images/regent/{name}.webp");
}
