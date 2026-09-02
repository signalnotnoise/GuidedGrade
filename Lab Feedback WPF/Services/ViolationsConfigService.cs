using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Service for loading violation terms from configuration files.
    /// Supports loading from a .violations file in the opened directory,
    /// falling back to violations.config in the app directory, or using defaults.
    /// </summary>
    public class ViolationsConfigService
    {
        private const string DirectoryConfigFileName = ".violations";
        private const string AppConfigFileName = "violations.config";

        private static readonly List<string> DefaultViolations = new()
        {
            "&", "const", "static_cast", "reinterpret_cast", "dynamic_cast",
            "resize()", "ignore()", "clear()", "auto", "try", "size_t", "goto",
            "catch", "\\0", "(...)", "var", "continue", "iterator"
        };

        private static readonly List<string> DefaultFilePatterns = new()
        {
            "*.cpp", "*.h", "*.cs", "*.c", "*.hpp"
        };

        public List<string> FilePatterns { get; private set; } = new(DefaultFilePatterns);

        /// <summary>
        /// Loads violation terms with priority: directory config > app config > defaults
        /// Also loads file patterns from the same configuration files.
        /// </summary>
        /// <param name="openedDirectoryPath">Path to the directory that was opened (optional)</param>
        /// <returns>List of violation terms to search for</returns>
        public List<string> LoadViolations(string? openedDirectoryPath = null)
        {
            // Priority 1: Check for .violations file in the opened directory
            if (!string.IsNullOrEmpty(openedDirectoryPath))
            {
                var directoryConfigPath = Path.Combine(openedDirectoryPath, DirectoryConfigFileName);
                if (File.Exists(directoryConfigPath))
                {
                    var (violations, patterns) = LoadFromFile(directoryConfigPath);
                    if (violations.Count > 0)
                    {
                        FilePatterns = patterns.Count > 0 ? patterns : new List<string>(DefaultFilePatterns);
                        return violations;
                    }
                }
            }

            // Priority 2: Check for violations.config in the app directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var appConfigPath = Path.Combine(appDirectory, AppConfigFileName);
            if (File.Exists(appConfigPath))
            {
                var (violations, patterns) = LoadFromFile(appConfigPath);
                if (violations.Count > 0)
                {
                    FilePatterns = patterns.Count > 0 ? patterns : new List<string>(DefaultFilePatterns);
                    return violations;
                }
            }

            // Priority 3: Use default hardcoded violations
            FilePatterns = new List<string>(DefaultFilePatterns);
            return new List<string>(DefaultViolations);
        }

        /// <summary>
        /// Loads violations from a file. Each line is treated as a separate violation term.
        /// Lines starting with # are treated as comments and ignored.
        /// Lines starting with [FILES] begin the file patterns section.
        /// Blank lines are ignored.
        /// </summary>
        /// <param name="filePath">Path to the violations configuration file</param>
        /// <returns>Tuple of (violation terms, file patterns)</returns>
        private (List<string> violations, List<string> patterns) LoadFromFile(string filePath)
        {
            var violations = new List<string>();
            var patterns = new List<string>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                var inFilesSection = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    {
                        continue;
                    }

                    // Check for [FILES] section marker
                    if (trimmedLine.Equals("[FILES]", StringComparison.OrdinalIgnoreCase))
                    {
                        inFilesSection = true;
                        continue;
                    }

                    // Check for end of [FILES] section (another section or end of special sections)
                    if (inFilesSection && trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        inFilesSection = false;
                        continue;
                    }

                    // Add to appropriate list based on current section
                    if (inFilesSection)
                    {
                        patterns.Add(trimmedLine);
                    }
                    else
                    {
                        violations.Add(trimmedLine);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error or handle gracefully - return empty lists to fall back to next priority
                System.Diagnostics.Debug.WriteLine($"Error loading violations from {filePath}: {ex.Message}");
            }

            return (violations, patterns);
        }

        /// <summary>
        /// Gets the default violation terms
        /// </summary>
        public List<string> GetDefaultViolations()
        {
            return new List<string>(DefaultViolations);
        }

        /// <summary>
        /// Checks if a file should be scanned based on the configured file patterns.
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the file matches any configured pattern</returns>
        public bool ShouldScanFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fileName = Path.GetFileName(filePath);

            foreach (var pattern in FilePatterns)
            {
                if (MatchesPattern(fileName, pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Matches a filename against a wildcard pattern (e.g., *.cpp, test*.cs).
        /// </summary>
        private bool MatchesPattern(string fileName, string pattern)
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Creates a sample violations configuration file at the specified path
        /// </summary>
        /// <param name="filePath">Path where the sample file should be created</param>
        public void CreateSampleConfigFile(string filePath)
        {
            var sampleContent = @"# Violations Configuration File
# Each line represents a term to flag as a violation in student code
# Lines starting with # are comments and will be ignored
# Blank lines are also ignored

# Pointers and references
&
const

# C++ casts
static_cast
reinterpret_cast
dynamic_cast

# Container methods
resize()
ignore()
clear()

# Keywords
auto
try
catch
goto
var
continue
iterator

# Special characters
\0
(...)

# File Patterns Section
# Use [FILES] to specify which files should be scanned for violations
# Supports wildcards: * (any characters) and ? (single character)
# If not specified, defaults to *.cpp, *.h, *.cs, *.c, *.hpp

[FILES]
*.cpp
*.h
*.cs
*.c
*.hpp
";
            File.WriteAllText(filePath, sampleContent);
        }
    }
}
