namespace GuidedGrade.Models
{
    public class Deduction
    {
        public string Label { get; init; }
        public float Points { get; init; }
        public bool IsAutoZero { get; init; }
        public bool IsBadSubmission { get; init; }
        public bool IsApplied { get; set; }

        public Deduction(string label, float points, bool isAutoZero = false, bool isBadSubmission = false)
        {
            Label = label;
            Points = points;
            IsAutoZero = isAutoZero;
            IsBadSubmission = isBadSubmission;
        }
    }
}