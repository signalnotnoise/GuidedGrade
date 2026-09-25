using Lab_Feedback_WPF.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lab_Feedback_WPF.Services
{
    public class ViolationsMatcher
    {
        private List<string> _searchStrings;

        public ViolationsMatcher(List<string> searchStrings)
        {
            _searchStrings = searchStrings ?? throw new ArgumentNullException(nameof(searchStrings));
        }

        /// <summary>
        /// Finds total number of instances of any search strings in the given text (case-sensitive)
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <returns>Total count of all matches</returns>
        public int CountMatches(string text)
        {
            return CountMatches(text, StringComparison.Ordinal);
        }

        /// <summary>
        /// Finds total number of instances of any search strings in the given text
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="comparisonType">String comparison type (case-sensitive, case-insensitive, etc.)</param>
        /// <returns>Total count of all matches</returns>
        public int CountMatches(string text, StringComparison comparisonType)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var allMatches = GetAllMatches(text, comparisonType);
            return allMatches.Count;
        }

        /// <summary>
        /// Gets detailed match information including which strings were found and how many times
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="comparisonType">String comparison type</param>
        /// <returns>Dictionary with search string as key and count as value</returns>
        public Dictionary<string, int> GetMatchDetails(string text, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var results = new Dictionary<string, int>();

            if (string.IsNullOrEmpty(text))
                return results;

            var allMatches = GetAllMatches(text, comparisonType);

            foreach (var match in allMatches)
            {
                if (results.ContainsKey(match.SearchString))
                    results[match.SearchString]++;
                else
                    results[match.SearchString] = 1;
            }

            return results;
        }

        /// <summary>
        /// Updates the list of search strings
        /// </summary>
        /// <param name="searchStrings">New list of strings to search for</param>
        public void UpdateSearchStrings(List<string> searchStrings)
        {
            _searchStrings = searchStrings ?? throw new ArgumentNullException(nameof(searchStrings));
        }

        /// <summary>
        /// Adds a new search string to the existing list
        /// </summary>
        /// <param name="searchString">String to add</param>
        public void AddSearchString(string searchString)
        {
            if (!string.IsNullOrEmpty(searchString))
            {
                _searchStrings.Add(searchString);
            }
        }

        /// <summary>
        /// Gets the current list of search strings
        /// </summary>
        public List<string> GetSearchStrings()
        {
            return new List<string>(_searchStrings);
        }

        /// <summary>
        /// Gets the context around each match as a single formatted string
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="contextLength">Number of characters to show before and after each match</param>
        /// <param name="comparisonType">String comparison type</param>
        /// <returns>Formatted string showing all matches with their context</returns>
        public string GetMatchesWithContext(string text, int contextLength = 30, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(text))
                return "No text provided.";

            var allMatches = GetAllMatches(text, comparisonType);

            if (allMatches.Count == 0)
                return "No matches found.";

            var result = new System.Text.StringBuilder();
            result.AppendLine($"Found {allMatches.Count} match(es):\n");

            for (int i = 0; i < allMatches.Count; i++)
            {
                var match = allMatches[i];

                // Calculate context boundaries
                int contextStart = Math.Max(0, match.Position - contextLength);
                int contextEnd = Math.Min(text.Length, match.Position + match.Length + contextLength);

                // Extract context
                string beforeContext = text.Substring(contextStart, match.Position - contextStart);
                string matchedText = text.Substring(match.Position, match.Length);
                string afterContext = text.Substring(match.Position + match.Length, contextEnd - (match.Position + match.Length));

                // Add ellipsis if context is truncated
                string beforeEllipsis = contextStart > 0 ? "..." : "";
                string afterEllipsis = contextEnd < text.Length ? "..." : "";

                // Format the result
                result.AppendLine($"Match #{i + 1}: '{match.SearchString}' at position {match.Position}");
                result.AppendLine($"Context: {beforeEllipsis}{beforeContext}[{matchedText}]{afterContext}{afterEllipsis}");
                result.AppendLine();
            }

            return result.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets matches with context as a list of structured objects
        /// </summary>
        /// <param name="text">Text to search in</param>
        /// <param name="contextLength">Number of characters to show before and after each match</param>
        /// <param name="comparisonType">String comparison type</param>
        /// <returns>List of match context objects</returns>
        public List<MatchContext> GetMatchContexts(string text, int contextLength = 30, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var results = new List<MatchContext>();

            if (string.IsNullOrEmpty(text))
                return results;

            var allMatches = GetAllMatches(text, comparisonType);

            foreach (var match in allMatches)
            {
                // Calculate context boundaries
                int contextStart = Math.Max(0, match.Position - contextLength);
                int contextEnd = Math.Min(text.Length, match.Position + match.Length + contextLength);

                // Extract context
                string beforeContext = text.Substring(contextStart, match.Position - contextStart);
                string matchedText = text.Substring(match.Position, match.Length);
                string afterContext = text.Substring(match.Position + match.Length, contextEnd - (match.Position + match.Length));

                results.Add(new MatchContext
                {
                    SearchString = match.SearchString,
                    MatchedText = matchedText,
                    Position = match.Position,
                    BeforeContext = beforeContext,
                    AfterContext = afterContext,
                    FullContext = $"{(contextStart > 0 ? "..." : "")}{beforeContext}[{matchedText}]{afterContext}{(contextEnd < text.Length ? "..." : "")}"
                });
            }

            return results;
        }

        private List<MatchInfo> GetAllMatches(string text, StringComparison comparisonType)
        {
            if (string.IsNullOrEmpty(text))
                return new List<MatchInfo>();

            // Strip comments so terms like "try" are not flagged when they appear in comments.
            // Positions in searchableText correspond 1-to-1 with the original text.
            string searchableText = StripComments(text);

            // Get all search strings sorted by length (longest first) to prioritize longer matches
            var sortedSearchStrings = _searchStrings.Where(s => !string.IsNullOrEmpty(s))
                                                   .OrderByDescending(s => s.Length)
                                                   .ToList();

            var allMatches = new List<MatchInfo>();

            foreach (string searchString in sortedSearchStrings)
            {
                // Terms like "resize()" match any call to that method, regardless of arguments
                bool isMethodPattern = searchString.EndsWith("()") && searchString.Length > 2
                    && searchString[..^2].All(c => char.IsLetterOrDigit(c) || c == '_');
                string effectiveSearch = isMethodPattern ? searchString[..^2] : searchString;

                int startIndex = 0;
                while (startIndex < searchableText.Length)
                {
                    int index = searchableText.IndexOf(effectiveSearch, startIndex, comparisonType);
                    if (index == -1)
                        break;

                    bool matched;
                    if (isMethodPattern)
                    {
                        int afterName = index + effectiveSearch.Length;
                        matched = afterName < searchableText.Length && searchableText[afterName] == '('
                            && IsWordBoundaryBefore(searchableText, index);
                    }
                    else
                    {
                        matched = IsExactMatch(searchableText, searchString, index);
                    }

                    if (matched)
                    {
                        allMatches.Add(new MatchInfo
                        {
                            SearchString = searchString,
                            Position = index,
                            Length = effectiveSearch.Length
                        });
                    }

                    startIndex = index + 1;
                }
            }

            // Sort by position
            allMatches.Sort((x, y) => x.Position.CompareTo(y.Position));

            // Remove overlapping matches (prioritize longer matches)
            var validMatches = new List<MatchInfo>();

            foreach (var match in allMatches)
            {
                bool overlaps = false;
                foreach (var existing in validMatches)
                {
                    // Check if this match overlaps with an existing match
                    if (match.Position < existing.Position + existing.Length &&
                        match.Position + match.Length > existing.Position)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    validMatches.Add(match);
                }
            }

            return validMatches;
        }

        /// <summary>
        /// Finds all violations in the specified text and returns their details, including line number, matched text,
        /// context, and line content.
        /// </summary>
        /// <remarks>Each violation includes the line number where it occurs, the matched text, the
        /// surrounding context, and the content of the line containing the violation.</remarks>
        /// <param name="text">The text to analyze for violations. Cannot be null or empty.</param>
        /// <param name="contextLength">The number of characters to include before and after each violation as context. Must be non-negative. The
        /// default is 50.</param>
        /// <returns>A list of violations found in the text. The list is empty if no violations are detected.</returns>
        public List<Violation> GetViolations(string text, int contextLength = 50)
        {
            if (string.IsNullOrEmpty(text)) return new List<Violation>();

            var contexts = GetMatchContexts(text, contextLength);
            var lines = text.Split('\n');

            return contexts.Select(ctx =>
            {
                // Calculate line number from character position
                var textUpToMatch = text[..ctx.Position];
                var lineNumber = textUpToMatch.Count(c => c == '\n') + 1;
                var lineContent = lineNumber <= lines.Length
                    ? lines[lineNumber - 1].Trim()
                    : string.Empty;

                return new Violation(lineNumber, ctx.MatchedText, ctx.FullContext, lineContent);
            }).ToList();
        }

        // Replaces C++ comment content with spaces so positions are preserved.
        // String literals are tracked to avoid treating // or /* inside strings as comments.
        private string StripComments(string text)
        {
            var result = text.ToCharArray();
            int i = 0;
            bool inString = false;
            bool inChar = false;

            while (i < text.Length)
            {
                if (inString)
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i += 2;
                    else if (text[i] == '"') { inString = false; i++; }
                    else i++;
                }
                else if (inChar)
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i += 2;
                    else if (text[i] == '\'') { inChar = false; i++; }
                    else i++;
                }
                else if (text[i] == '"') { inString = true; i++; }
                else if (text[i] == '\'') { inChar = true; i++; }
                else if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') { result[i] = ' '; i++; }
                }
                else if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
                {
                    result[i] = ' '; result[i + 1] = ' '; i += 2;
                    while (i < text.Length)
                    {
                        if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '/')
                        {
                            result[i] = ' '; result[i + 1] = ' '; i += 2; break;
                        }
                        if (text[i] != '\n') result[i] = ' ';
                        i++;
                    }
                }
                else i++;
            }

            return new string(result);
        }

        private bool IsWordBoundaryBefore(string text, int index)
        {
            if (index == 0) return true;
            char charBefore = text[index - 1];
            return !char.IsLetterOrDigit(charBefore) && charBefore != '_';
        }

        private bool IsExactMatch(string text, string searchString, int index)
        {
            // For alphanumeric strings (words), check word boundaries
            if (searchString.Any(c => char.IsLetterOrDigit(c)))
            {
                // Check character before the match
                if (index > 0)
                {
                    char charBefore = text[index - 1];
                    if (char.IsLetterOrDigit(charBefore) || charBefore == '_')
                    {
                        return false;
                    }
                }

                // Check character after the match
                if (index + searchString.Length < text.Length)
                {
                    char charAfter = text[index + searchString.Length];
                    if (char.IsLetterOrDigit(charAfter) || charAfter == '_')
                    {
                        return false;
                    }
                }
            }
            else
            {
                // For symbol-only strings, check that they're not part of longer symbol sequences
                // Check character before the match
                if (index > 0)
                {
                    char charBefore = text[index - 1];
                    // If the character before is the same type of symbol, it's not an exact match
                    if (!char.IsWhiteSpace(charBefore) && !char.IsLetterOrDigit(charBefore))
                    {
                        return false;
                    }
                }

                // Check character after the match
                if (index + searchString.Length < text.Length)
                {
                    char charAfter = text[index + searchString.Length];
                    // If the character after is the same type of symbol, it's not an exact match
                    if (!char.IsWhiteSpace(charAfter) && !char.IsLetterOrDigit(charAfter))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Helper class for match information
        private class MatchInfo
        {
            public required string SearchString { get; set; }
            public int Position { get; set; }
            public int Length { get; set; }
        }
    }

    /// <summary>
    /// Represents a match with its surrounding context
    /// </summary>
    public class MatchContext
    {
        public required string SearchString { get; set; }
        public required string MatchedText { get; set; }
        public int Position { get; set; }
        public required string BeforeContext { get; set; }
        public required string AfterContext { get; set; }
        public required string FullContext { get; set; }
    }
}
