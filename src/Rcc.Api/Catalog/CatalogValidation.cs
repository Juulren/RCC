using System.Text.RegularExpressions;

namespace Rcc.Api.Catalog;

public static partial class CatalogValidation
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
    [GeneratedRegex("^[A-Z0-9-]{1,12}$")]
    private static partial Regex NumberPattern();
    public static string Clean(string? value) => value?.Trim() ?? "";
    public static Dictionary<string, string[]> Hotel(HotelInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Text(errors, "name", input.Name, 180);
        Text(errors, "descriptionEs", input.DescriptionEs, 4000);
        Text(errors, "descriptionEn", input.DescriptionEn, 4000);
        Text(errors, "address", input.Address, 500);
        if (input.Latitude is < -90 or > 90 || decimal.Round(input.Latitude, 6) != input.Latitude) errors["latitude"] = ["OUT_OF_RANGE"];
        if (input.Longitude is < -180 or > 180 || decimal.Round(input.Longitude, 6) != input.Longitude) errors["longitude"] = ["OUT_OF_RANGE"];
        if (input.TimeZone != "Asia/Shanghai") errors["timeZone"] = ["INVALID_VALUE"];
        if (input.Services is null || input.Services.Length > 20) errors["services"] = ["INVALID_VALUE"];
        else foreach (var (service, index) in input.Services.Select((service, index) => (service, index)))
        {
            Text(errors, $"services.{index}.nameEs", service?.NameEs, 100);
            Text(errors, $"services.{index}.nameEn", service?.NameEn, 100);
        }
        return errors;
    }
    public static Dictionary<string, string[]> Type(RoomTypeInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Text(errors, "slug", input.Slug, 80);
        if (!SlugPattern().IsMatch(Clean(input.Slug))) errors["slug"] = ["INVALID_VALUE"];
        Text(errors, "nameEs", input.NameEs, 180);
        Text(errors, "nameEn", input.NameEn, 180);
        Text(errors, "descriptionEs", input.DescriptionEs, 4000);
        Text(errors, "descriptionEn", input.DescriptionEn, 4000);
        if (input.Category is not ("room" or "suite")) errors["category"] = ["INVALID_VALUE"];
        if (input.Beds is not ("king" or "choice")) errors["beds"] = ["INVALID_VALUE"];
        if (input.Area is < 1 or > 1000) errors["area"] = ["OUT_OF_RANGE"];
        if (input.Capacity is < 1 or > 12) errors["capacity"] = ["OUT_OF_RANGE"];
        if (input.MaxAdults < 1 || input.MaxAdults > input.Capacity) errors["maxAdults"] = ["OUT_OF_RANGE"];
        if (input.MaxChildren < 0 || input.MaxChildren >= input.Capacity) errors["maxChildren"] = ["OUT_OF_RANGE"];
        if (input.NightlyRate is <= 0 or > 9999999.99m || decimal.Round(input.NightlyRate, 2) != input.NightlyRate) errors["nightlyRate"] = ["OUT_OF_RANGE"];
        if (input.SortOrder is < 0 or > 9999) errors["sortOrder"] = ["OUT_OF_RANGE"];
        if (input.Images is not { Length: >= 1 and <= 12 }) errors["images"] = ["INVALID_VALUE"];
        else
        {
            if (input.Images.Select(image => image?.Url).Distinct().Count() != input.Images.Length) errors["images"] = ["DUPLICATE"];
            foreach (var (image, index) in input.Images.Select((image, index) => (image, index)))
            {
                if (!CatalogAssets.IsApproved(image?.Url)) errors[$"images.{index}.url"] = ["INVALID_ASSET"];
                Text(errors, $"images.{index}.altEs", image?.AltEs, 250);
                Text(errors, $"images.{index}.altEn", image?.AltEn, 250);
            }
        }
        return errors;
    }
    public static Dictionary<string, string[]> Room(RoomInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (!NumberPattern().IsMatch(Clean(input.Number).ToUpperInvariant())) errors["number"] = ["INVALID_VALUE"];
        if (input.RoomTypeId == Guid.Empty) errors["roomTypeId"] = ["REQUIRED"];
        return errors;
    }
    private static void Text(Dictionary<string, string[]> errors, string key, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) errors[key] = ["INVALID_VALUE"];
    }
}
