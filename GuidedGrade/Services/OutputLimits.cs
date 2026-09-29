namespace GuidedGrade.Services;

internal static class OutputLimits
{
    public const int ConsoleCharacters = 64_000;
    public const int BuildCharacters = 8_000;
    public const int PendingTerminalCharacters = 16_000;
    public const int PendingTerminalChunks = 128;
    public const int TerminalCharacters = 100_000;
    public const int TerminalRuns = 256;
    public const string TruncationMarker = "\n...[output truncated]...\n";
}
