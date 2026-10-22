using System.Text.RegularExpressions;
namespace Rcc.Api.Recommendations;

public static class RelayAddress
{
    // Only the explicitly configured HTTPS demo tunnel. Never attach a credential to an arbitrary URL.
    public static bool IsValid(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && Regex.IsMatch(uri.IdnHost, "^[a-z0-9-]+\\.trycloudflare\\.com$", RegexOptions.CultureInvariant);
}
public sealed record RelayCandidate(string Id, decimal TotalCny, int Rooms, int RiverRooms, int TotalArea);
public sealed record RelayInput(string Locale, string[] Preferences, RelayCandidate[] Candidates)
{
    public bool IsValid() => Locale is "es" or "en" && Preferences is { Length: <= 3 }
        && Preferences.Distinct().Count() == Preferences.Length && Preferences.All(p => p is "river" or "space" or "value")
        && Candidates is { Length: >= 1 and <= 12 } && Candidates.All(c => c is not null
            && c.Id is not null && Regex.IsMatch(c.Id, "^option-[0-9]{1,2}$", RegexOptions.CultureInvariant)
            && c.TotalCny is > 0 and <= 100000000 && decimal.Round(c.TotalCny, 2) == c.TotalCny
            && c.Rooms is >= 1 and <= 8 && c.RiverRooms >= 0 && c.RiverRooms <= c.Rooms && c.TotalArea is > 0 and <= 100000)
        && Candidates.Select(c => c.Id).Distinct().Count() == Candidates.Length;
    public List<Candidate> ToCandidates() => Candidates.Select(c => new Candidate(c.Id, new Guid[c.Rooms], c.TotalCny, c.RiverRooms, c.TotalArea, [])).ToList();
}
