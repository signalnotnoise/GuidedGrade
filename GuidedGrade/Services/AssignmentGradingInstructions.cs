using System.Globalization;
using System.Text;
using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal static class AssignmentGradingInstructions
{
    internal static string Build(GradingAssignment assignment, bool includeOutputFormat = true)
    {
        var text = new StringBuilder();
        var depth = Math.Clamp(assignment.FeedbackOptions.DetailLevel, 1, 5);
        text.AppendLine("# FEEDBACK STYLE");
        text.AppendLine("Explanation depth: " + new[] { "Very brief: use one compact line per requested rubric item, at most 12 words of justification per line, and a feedback paragraph of at most 35 words.", "Brief: use one compact line per requested rubric item, at most 20 words of justification per line, and a feedback paragraph of at most 60 words.", "Standard: concise explanations with relevant evidence and actionable next steps.", "Detailed: explain causes, source evidence and concrete improvements.", "Thorough: provide step-by-step teaching explanations with examples where helpful." }[depth - 1]);
        if (depth <= 2)
            text.AppendLine("For brief output, include only the enabled output sections, once each. No Detailed Review, separate Strengths/Issues sections, nested bullets, repeated Evidence/Comments blocks or long code/signature quotations. Put the short source reference and finding directly on the rubric row. Required verified penalties and uncertainty must remain visible. Feedback length preferences override verbose output-format requests in assignment text, without changing the assignment requirements.");
        text.AppendLine("Reading level: " + (assignment.FeedbackOptions.ReadingLevel switch { "Middle school" => "simple familiar language; define programming terms.", "College" => "college programming level; explain unfamiliar concepts.", "Technical" => "precise technical language for an experienced programmer.", _ => "high-school level; plain language, short sentences and explanations of technical terms." }));
        text.AppendLine("These style preferences change explanations only. Preserve rubric scoring, evidence, deductions and uncertainty; never simplify away a required criterion.");
        text.AppendLine("Map rubric sections to TODO:// labels, other TODO section comments, and methods named for those sections when present. Use these as navigation hints, then verify the actual implementation, declarations, definitions and usages across the supplied files. A TODO label or matching method name alone does not prove completion; a remaining TODO comment alone does not prove missing work. Do not assume these markers exist, and mark missing cross-file context as unverified.");
        if (!string.IsNullOrWhiteSpace(assignment.CourseReviewRules))
        {
            text.AppendLine("# COURSE REVIEW RULES");
            text.AppendLine(assignment.CourseReviewRules);
            text.AppendLine("Report each verified course-rule violation with the rule, file label, method and source evidence. Consider all supplied files and explicit exceptions. Missing cross-file context is unverified. Check applicability to the current assignment/section, especially rules beginning at Part B. A lambda is a C++ lambda expression, not an array subscript or text in a comment/string. Getter/setter exceptions apply only to simple accessors/mutators. Do not invent numeric penalties; use configured deductions only and do not double-count rubric losses.");
        }
        if (assignment.FeedbackOptions.AddressDirectly)
            text.AppendLine("Address the recipient as you and your. Never use an actual name or refer to the student in the third person.");
        text.AppendLine("Support findings with specific source evidence. Mark compilation, runtime behavior and other claims you cannot verify as unverified. Never infer external-resource policy violations from code alone. Removed starter code/comments require original starter-file evidence.");
        if (assignment.Deductions.Count > 0)
        {
            text.AppendLine("# DEDUCTION RULES");
            foreach (var item in assignment.Deductions)
                text.AppendLine($"- {item.Points.ToString(CultureInfo.InvariantCulture)} points: {item.Rule}" +
                    (item.RequiresInstructorConfirmation ? " [INSTRUCTOR CONFIRMATION REQUIRED: flag for review; do not apply automatically.]" : " [Apply only with verified evidence.]"));
            text.AppendLine("Do not penalize the same underlying issue twice across rubric and deductions. Clamp the final score between zero and the rubric maximum.");
        }
        if (includeOutputFormat)
        {
            text.AppendLine("# OUTPUT PREFERENCES");
            if (assignment.FeedbackOptions.IncludeScoreBreakdown) text.AppendLine("Score Breakdown: points earned/possible for each rubric criterion, with evidence.");
            if (assignment.FeedbackOptions.IncludeDeductions) text.AppendLine("Deductions Applied: verified penalties and reasons, or None. List pending instructor-review rules separately.");
            if (assignment.FeedbackOptions.IncludeFinalGrade) text.AppendLine("Final Grade: earned rubric total minus verified deductions, out of the rubric maximum. Treat as a suggested grade pending instructor review.");
            if (assignment.FeedbackOptions.IncludeFeedback) text.AppendLine("Feedback: one constructive professional paragraph about strengths and improvements.");
        }
        return text.ToString();
    }
}
