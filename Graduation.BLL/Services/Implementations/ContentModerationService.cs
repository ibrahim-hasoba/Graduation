using System.Text.Json;
using System.Text.RegularExpressions;
using Graduation.BLL.Services.Interfaces;

namespace Graduation.BLL.Services.Implementations
{
    public class ContentModerationService : IContentModerationService
    {
        private readonly HashSet<string> _badWordsEn;
        private readonly HashSet<string> _badWordsAr;
        private readonly int _maxUrls;
        private readonly int _maxAllCapsWords;
        private readonly int _maxGibberishSequences;
        private readonly int _phoneThreshold;
        private readonly int _repeatedCharThreshold;

        private static readonly Regex UrlPattern = new(
            @"https?://[^\s]+|www\.[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RepeatedCharsPattern = new(
            @"(.)\1{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PhonePattern = new(
            @"(\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{2,4}[-.\s]?\d{2,9}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AllCapsWordPattern = new(
            @"\b[A-Z]{4,}\b", RegexOptions.Compiled);

        private static readonly Regex GibberishPattern = new(
            @"(?:[^aeiouyAEIOUY\s]{5,})", RegexOptions.Compiled);

        public ContentModerationService(string badWordsFilePath)
        {
            BadWordsData data;
            try
            {
                var json = File.ReadAllText(badWordsFilePath);
                data = JsonSerializer.Deserialize<BadWordsData>(json) ?? new BadWordsData();
            }
            catch
            {
                data = new BadWordsData();
            }

            _badWordsEn = new HashSet<string>(data.English ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            _badWordsAr = new HashSet<string>(data.Arabic ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            _maxUrls = data.SpamPatterns?.MaxUrls ?? 2;
            _maxAllCapsWords = data.SpamPatterns?.MaxAllCapsWords ?? 3;
            _maxGibberishSequences = data.SpamPatterns?.MaxGibberishConsonantSequences ?? 2;
            _phoneThreshold = data.SpamPatterns?.PhoneNumberThreshold ?? 1;
            _repeatedCharThreshold = data.SpamPatterns?.RepeatedCharThreshold ?? 3;
        }

        public ContentModerationResult Moderate(string text)
        {
            var result = new ContentModerationResult();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '_', '@', '#', '$', '%', '&', '*', '(', ')', '[', ']', '{', '}', '/', '\\', '|', '<', '>', '+', '=', '~', '`' }, StringSplitOptions.RemoveEmptyEntries);

            var badWordsFound = words.Where(w => _badWordsEn.Contains(w)).ToList();
            if (badWordsFound.Any())
            {
                result.IsClean = false;
                result.Flags.Add($"Contains inappropriate language: {string.Join(", ", badWordsFound.Distinct())}");
            }

            var badWordsArFound = words.Where(w => _badWordsAr.Contains(w)).ToList();
            if (badWordsArFound.Any())
            {
                result.IsClean = false;
                result.Flags.Add($"Contains inappropriate Arabic language: {string.Join(", ", badWordsArFound.Distinct())}");
            }

            var urls = UrlPattern.Matches(text);
            if (urls.Count > _maxUrls)
            {
                result.IsClean = false;
                result.Flags.Add($"Contains {urls.Count} URLs (max {_maxUrls} allowed)");
            }

            var repeatedMatches = RepeatedCharsPattern.Matches(text);
            foreach (Match match in repeatedMatches)
            {
                if (match.Length > _repeatedCharThreshold)
                {
                    result.IsClean = false;
                    result.Flags.Add("Contains suspicious repeated characters");
                    break;
                }
            }

            if (PhonePattern.Matches(text).Count > _phoneThreshold)
            {
                result.IsClean = false;
                result.Flags.Add("Contains phone numbers in text fields");
            }

            var allCapsWords = AllCapsWordPattern.Matches(text);
            if (allCapsWords.Count > _maxAllCapsWords)
            {
                result.IsClean = false;
                result.Flags.Add("Contains excessive SHOUTING (all-caps words)");
            }

            var gibberishCount = GibberishPattern.Matches(text).Count;
            if (gibberishCount > _maxGibberishSequences)
            {
                result.IsClean = false;
                result.Flags.Add("Contains gibberish text");
            }

            return result;
        }

        private class BadWordsData
        {
            public List<string>? English { get; set; }
            public List<string>? Arabic { get; set; }
            public SpamPatternsData? SpamPatterns { get; set; }
        }

        private class SpamPatternsData
        {
            public int MaxUrls { get; set; }
            public int MaxAllCapsWords { get; set; }
            public int MaxGibberishConsonantSequences { get; set; }
            public int PhoneNumberThreshold { get; set; }
            public int RepeatedCharThreshold { get; set; }
        }
    }
}
