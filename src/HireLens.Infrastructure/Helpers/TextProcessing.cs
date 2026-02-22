using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HireLens.Infrastructure.Helpers;

internal static partial class TextProcessing
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and",
        "the",
        "with",
        "for",
        "you",
        "your",
        "from",
        "that",
        "this",
        "have",
        "has",
        "are",
        "was",
        "were",
        "will",
        "can",
        "into",
        "our",
        "their",
        "they",
        "them",
        "who",
        "how",
        "what",
        "when",
        "where",
        "about",
        "years",
        "year",
        "work",
        "experience",
        "team",
        "strong",
        "skills"
    };

    public static IReadOnlyList<string> ParseSkillList(string? rawSkillText)
    {
        if (string.IsNullOrWhiteSpace(rawSkillText))
        {
            return [];
        }

        var values = rawSkillText
            .Split(new[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSkill)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values;
    }

    public static string JoinSkillList(IEnumerable<string> skills)
    {
        return string.Join(", ", ParseSkillList(string.Join(", ", skills)));
    }

    public static IReadOnlyList<string> TokenizeKeywords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = NormalizeText(text);
        var words = WordRegex()
            .Matches(normalized)
            .Select(x => x.Value.Trim())
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return words;
    }

    public static string NormalizeSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
        {
            return string.Empty;
        }

        var compact = SpaceRegex().Replace(skill.Trim(), " ");
        return compact;
    }

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[a-z0-9\+#\.-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WordRegex();
}
