using System.Collections.Generic;

namespace Lab_Feedback_WPF.Models
{
    /// <summary>
    /// Represents a single rubric item with point value
    /// </summary>
    public class RubricItem
    {
        public string Name { get; set; }
        public double MaxPoints { get; set; }
        public double EarnedPoints { get; set; }
        public string Feedback { get; set; }
        public bool IsGraded { get; set; }

        public RubricItem(string name, double maxPoints)
        {
            Name = name;
            MaxPoints = maxPoints;
            EarnedPoints = 0;
            Feedback = "";
            IsGraded = false;
        }
    }

    /// <summary>
    /// Represents a grading assignment with requirements and rubric
    /// </summary>
    public class GradingAssignment
    {
        public string Course { get; set; }
        public string Title { get; set; }
        public string Requirements { get; set; }
        public List<RubricItem> Rubric { get; set; }

        public GradingAssignment()
        {
            Course = "General";
            Title = "";
            Requirements = "";
            Rubric = new List<RubricItem>();
        }

        public double TotalMaxPoints
        {
            get
            {
                double total = 0;
                foreach (var item in Rubric)
                    total += item.MaxPoints;
                return total;
            }
        }

        public double TotalEarnedPoints
        {
            get
            {
                double total = 0;
                foreach (var item in Rubric)
                    total += item.EarnedPoints;
                return total;
            }
        }

        public double PercentageScore => TotalMaxPoints > 0 
            ? (double)TotalEarnedPoints / TotalMaxPoints * 100 
            : 0;
    }

    /// <summary>
    /// Feedback for a specific code section with suggested fixes
    /// </summary>
    public class SectionFeedback
    {
        public bool IsOverallReview { get; set; }
        public string SectionName { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Strengths { get; set; }
        public string SuggestedCode { get; set; }
        public double SuggestedScore { get; set; }
        public string Explanation { get; set; }
        public FeedbackReviewStatus ReviewStatus { get; set; }

        public SectionFeedback()
        {
            SectionName = string.Empty;
            Issues = new List<string>();
            Strengths = new List<string>();
            SuggestedCode = "";
            Explanation = string.Empty;
            ReviewStatus = FeedbackReviewStatus.Pending;
        }
    }

    public enum FeedbackReviewStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
