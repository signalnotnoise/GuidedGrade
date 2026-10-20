namespace GuidedGrade.ViewModels;
internal sealed record BuildHistoryPoint(int Number, DateTimeOffset Time, TimeSpan? Gap, int FileCount);
internal sealed class BuildHistoryViewModel
{
    internal IReadOnlyList<BuildHistoryPoint> Points { get; set; } = [];
    internal Action<int>? SelectBuild { get; set; }
}
