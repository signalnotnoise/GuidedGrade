using GuidedGrade.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GuidedGrade.Services
{
    internal class FileHandler
    {
        public static string SearchFile(string path, string filename)
        {
           
            var files = Directory.GetFiles(path, filename, SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0]; // Return the first found file
            }

            return "";

            // throw new FileNotFoundException($"File '{filename}' not found in path '{path}'");
        }

        public static List<Result> ParseFile(string filepath)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
            {
                System.Diagnostics.Debug.WriteLine($"ParseFile: File not found or empty path: {filepath}");
                return results;
            }

            if (Path.GetExtension(filepath).Equals(".fslog", StringComparison.OrdinalIgnoreCase))
            {
                var log = FsLogReader.Read(filepath);
                return [new Result("Builds from " + Path.GetFileName(filepath), log.BuildCount)];
            }
            var lines = File.ReadAllLines(filepath);
            var extension = Path.GetExtension(filepath).ToLower();
            var filename = Path.GetFileName(filepath).ToLower();

            System.Diagnostics.Debug.WriteLine($"ParseFile: Processing {filepath} ({lines.Length} lines, extension: {extension})");

            // Handle .fslog, .log, or output.txt files - count build attempts
            if (extension == ".fslog" || extension == ".log" || filename == "output.txt")
            {
                int buildCount = 0;
                int lastScore = 0;

                System.Diagnostics.Debug.WriteLine($"ParseFile: Using build log parser for {filename}");

                foreach (var line in lines)
                {
                    // Look for build/compile indicators
                    if (line.Contains("Build started", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Building", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Rebuild started", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("========== Build:", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("1>------ Build started:", StringComparison.OrdinalIgnoreCase))
                    {
                        buildCount++;
                        System.Diagnostics.Debug.WriteLine($"  Build #{buildCount} detected: {line.Trim()}");
                    }

                    // Try to extract score if present (format: "Score: 85" or "Score - 85")
                    if (line.Contains("score", StringComparison.OrdinalIgnoreCase))
                    {
                        var scoreParts = line.Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        if (scoreParts.Length >= 2 && int.TryParse(scoreParts[1].Trim(), out var score))
                        {
                            lastScore = score;
                            System.Diagnostics.Debug.WriteLine($"  Score found: {lastScore}");
                        }
                    }
                }

                if (buildCount > 0)
                {
                    results.Add(new Result($"Builds from {Path.GetFileName(filepath)}", buildCount));
                    System.Diagnostics.Debug.WriteLine($"ParseFile: Total builds found: {buildCount}");
                }

                if (lastScore > 0)
                {
                    results.Add(new Result("Last Score", lastScore));
                }
            }
            // Handle hdkvkt.txt or other text files with "Description - Number" format
            else if (extension == ".txt" && filename != "output.txt")
            {
                System.Diagnostics.Debug.WriteLine("ParseFile: Using legacy format parser (Description - Number)");
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var number))
                    {
                        results.Add(new Result(parts[0], number));
                        System.Diagnostics.Debug.WriteLine($"  Parsed: {parts[0]} - {number}");
                    }
                }
            }
            else
            {
                // Unknown format - try original parser as fallback
                System.Diagnostics.Debug.WriteLine("ParseFile: Unknown format, trying legacy parser");
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var number))
                    {
                        results.Add(new Result(parts[0], number));
                        System.Diagnostics.Debug.WriteLine($"  Parsed: {parts[0]} - {number}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"ParseFile: Returning {results.Count} results");
            return results;
        }

        public static List<string> SearchCppFiles(string path, List<string>? exclusions = null)
        {
            // If exclusions is null, initialize it as an empty HashSet
            var exclusionSet = exclusions != null
                ? new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();

            // List to hold the paths of the found .cpp files
            var cppFiles = new List<string>();

            // Search for all .cpp files in the specified path and subdirectories
            var files = Directory.GetFiles(path, "*.cpp", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // Get the file name without the directory path
                var fileName = Path.GetFileName(file);

                // Add the file to the list if it's not in the exclusions set
                if (!exclusionSet.Contains(fileName))
                {
                    cppFiles.Add(file);
                }
            }

            return cppFiles;
        }

        public static List<string> SearchHeaderFiles(string path, List<string>? exclusions = null)
        {
            // If exclusions is null, initialize it as an empty HashSet
            var exclusionSet = exclusions != null
                ? new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();

            // List to hold the paths of the found .cpp files
            var studentFiles = new List<string>();

            // Search for all .cpp and header files in the specified path and subdirectories
            var headerFiles = Directory.GetFiles(path, "*.h", SearchOption.AllDirectories);
            var hppFiles = Directory.GetFiles(path, "*.hpp", SearchOption.AllDirectories);
            var cppFiles = Directory.GetFiles(path, "*.cpp", SearchOption.AllDirectories);

            var files = headerFiles.Concat(cppFiles).Concat(hppFiles).ToArray();

            foreach (var file in files)
            {
                // Get the file name without the directory path
                var fileName = Path.GetFileName(file);

                // Add the file to the list if it's not in the exclusions set
                if (!exclusionSet.Contains(fileName))
                {
                    studentFiles.Add(file);
                }
            }

            return studentFiles;
        }

        public static string? GetSubfolderFromPath(string path, string? subFolder)
        {
            if (subFolder == null) return "";

            var index = subFolder.IndexOf(path, StringComparison.Ordinal);

            return index < 0 ? subFolder : subFolder.Remove(index, path.Length + 1);
        }

        public static LabResults? LoadLabResults(string assignmentPath)
        {
            var resultsPath = SearchFile(assignmentPath, "results.json");
            if (string.IsNullOrEmpty(resultsPath)) return null;

            var json = File.ReadAllText(resultsPath);
            return System.Text.Json.JsonSerializer.Deserialize<LabResults>(json);
        }
    }
}
