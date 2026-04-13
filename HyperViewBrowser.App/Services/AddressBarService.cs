using System.Text.RegularExpressions;
using HyperViewBrowser.App.Models;

namespace HyperViewBrowser.App.Services;

public class AddressBarService
{
    private static readonly Regex UrlLike = new(@"^(https?://|about:|file:///|localhost|\w+\.\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string NormalizeToUrl(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return "about:blank";
        if (UrlLike.IsMatch(input))
        {
            return input.Contains("://", StringComparison.Ordinal) || input.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                ? input
                : $"https://{input}";
        }

        var encoded = Uri.EscapeDataString(input);
        return $"https://www.bing.com/search?q={encoded}";
    }

    public IEnumerable<string> BuildSuggestions(string query, IEnumerable<HistoryItem> history, IEnumerable<BookmarkItem> bookmarks)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        query = query.Trim();

        var known = history.Select(x => x.Url)
            .Concat(bookmarks.Select(x => x.Url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(6);

        return known.Concat([NormalizeToUrl(query)]).Distinct(StringComparer.OrdinalIgnoreCase).Take(8);
    }
}
