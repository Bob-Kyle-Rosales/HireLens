using System.Text.RegularExpressions;

namespace HireLens.Infrastructure.Helpers;

internal static partial class ResumeScoringTextBuilder
{
    private static readonly IReadOnlyDictionary<string, ResumeSectionKind> SectionHeadings =
        new Dictionary<string, ResumeSectionKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["skills"] = ResumeSectionKind.Skills,
            ["technical skills"] = ResumeSectionKind.Skills,
            ["core skills"] = ResumeSectionKind.Skills,
            ["skill set"] = ResumeSectionKind.Skills,
            ["tech stack"] = ResumeSectionKind.Skills,
            ["technologies"] = ResumeSectionKind.Skills,
            ["tools"] = ResumeSectionKind.Skills,
            ["professional experience"] = ResumeSectionKind.Experience,
            ["work experience"] = ResumeSectionKind.Experience,
            ["experience"] = ResumeSectionKind.Experience,
            ["employment history"] = ResumeSectionKind.Experience,
            ["projects"] = ResumeSectionKind.Projects,
            ["project experience"] = ResumeSectionKind.Projects,
            ["key projects"] = ResumeSectionKind.Projects,
            ["summary"] = ResumeSectionKind.Summary,
            ["professional summary"] = ResumeSectionKind.Summary,
            ["profile"] = ResumeSectionKind.Summary,
            ["objective"] = ResumeSectionKind.Summary,
            ["education"] = ResumeSectionKind.Other,
            ["certifications"] = ResumeSectionKind.Other,
            ["certification"] = ResumeSectionKind.Other,
            ["training"] = ResumeSectionKind.Other,
            ["awards"] = ResumeSectionKind.Other,
            ["languages"] = ResumeSectionKind.Other,
            ["interests"] = ResumeSectionKind.Other,
            ["hobbies"] = ResumeSectionKind.Other,
            ["references"] = ResumeSectionKind.Other,
            ["contact"] = ResumeSectionKind.Other,
            ["contact information"] = ResumeSectionKind.Other
        };

    public static ResumeScoringProfile Build(string resumeText)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return new ResumeScoringProfile(string.Empty, string.Empty, false);
        }

        var lines = resumeText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return new ResumeScoringProfile(string.Empty, string.Empty, false);
        }

        var skillLines = new List<string>();
        var experienceLines = new List<string>();
        var projectLines = new List<string>();
        var summaryLines = new List<string>();
        ResumeSectionKind? currentSection = null;

        foreach (var line in lines)
        {
            if (TryGetSectionHeading(line, out var section))
            {
                currentSection = section;
                continue;
            }

            switch (currentSection)
            {
                case ResumeSectionKind.Skills:
                    skillLines.Add(line);
                    break;
                case ResumeSectionKind.Experience:
                    experienceLines.Add(line);
                    break;
                case ResumeSectionKind.Projects:
                    projectLines.Add(line);
                    break;
                case ResumeSectionKind.Summary:
                    summaryLines.Add(line);
                    break;
            }
        }

        var skillEvidenceText = CombineLines(skillLines, experienceLines, projectLines);
        if (string.IsNullOrWhiteSpace(skillEvidenceText))
        {
            var fallbackText = CombineLines(lines);
            return new ResumeScoringProfile(fallbackText, fallbackText, false);
        }

        var similarityText = CombineLines(skillLines, skillLines, experienceLines, projectLines, summaryLines);
        return new ResumeScoringProfile(similarityText, skillEvidenceText, true);
    }

    private static bool TryGetSectionHeading(string line, out ResumeSectionKind section)
    {
        var normalized = NormalizeHeading(line);
        return SectionHeadings.TryGetValue(normalized, out section);
    }

    private static string CleanLine(string line)
    {
        var withoutBulletPrefix = BulletPrefixRegex().Replace(line.Trim(), string.Empty);
        return WhitespaceRegex().Replace(withoutBulletPrefix, " ").Trim();
    }

    private static string NormalizeHeading(string line)
    {
        if (line.Length > 64)
        {
            return string.Empty;
        }

        var sanitized = HeadingNoiseRegex().Replace(line.Trim().ToLowerInvariant(), " ");
        return WhitespaceRegex().Replace(sanitized, " ").Trim();
    }

    private static string CombineLines(params IEnumerable<string>[] groups)
    {
        return string.Join(
            Environment.NewLine,
            groups.SelectMany(group => group)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    [GeneratedRegex(@"^(?:[-*]|\d+[.)])\s*")]
    private static partial Regex BulletPrefixRegex();

    [GeneratedRegex(@"[^a-z0-9\s/&+\-]")]
    private static partial Regex HeadingNoiseRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private enum ResumeSectionKind
    {
        Skills,
        Experience,
        Projects,
        Summary,
        Other
    }
}

internal sealed record ResumeScoringProfile(
    string SimilarityText,
    string SkillEvidenceText,
    bool UsesFocusedSections);
