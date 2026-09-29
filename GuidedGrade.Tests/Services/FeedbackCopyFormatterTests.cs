using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class FeedbackCopyFormatterTests
{
    [TestMethod]
    public void PlainTextPreservesEmphasisCharactersInsideInlineCode()
    {
        Assert.AreEqual("Use **pointer** and care", FeedbackCopyFormatter.Format("Use `**pointer**` and **care**", "TXT"));
    }

    [TestMethod]
    public void SavedSectionIncludesSuggestedCodeAndBothFeedbackCategories()
    {
        var feedback = new GuidedGrade.Models.SectionFeedback
        {
            SectionName = "Input", Explanation = "Validate the input.", SuggestedCode = "if (x < 0) return;",
            Strengths = new() { "Clear names" }, Issues = new() { "Missing validation" }
        };
        var result = SavedFeedbackText.Format(feedback);
        StringAssert.Contains(result, "Strengths:");
        StringAssert.Contains(result, "Areas to improve:");
        StringAssert.Contains(result, "if (x < 0) return;");
    }
    [TestMethod]
    public void ColorsExplicitCategoriesAndKeepsCodeLiteral()
    {
        var html = FeedbackCopyFormatter.Format("Strengths:\n- Correct result\n**Warnings:**\n- Check edge cases\n## Issues\n- Missing validation\n```\nStrengths:\n<script>\n```\n## Summary\nReview before publishing", "HTML");
        StringAssert.Contains(html, "#166534");
        StringAssert.Contains(html, "#854d0e");
        StringAssert.Contains(html, "#991b1b");
        StringAssert.Contains(html, "<li");
        StringAssert.Contains(html, "&lt;script&gt;");
        Assert.AreEqual(3, System.Text.RegularExpressions.Regex.Matches(html, "<section ").Count);
        StringAssert.Contains(html.Replace("\r\n", "\n"), "</section>\n<h2>Summary</h2>");
    }

    [TestMethod]
    public void FormatsEditedFeedbackAndEscapesHtml()
    {
        const string draft = "## Review\n**Good work**\n```cpp\nif (a < b) {}\n```\n<script>alert(1)</script>";
        Assert.AreEqual(draft, FeedbackCopyFormatter.Format(draft, "Markdown"));
        var plain = FeedbackCopyFormatter.Format(draft, "TXT");
        StringAssert.StartsWith(plain, "Review");
        Assert.IsFalse(plain.Contains("**") || plain.Contains("```"));
        StringAssert.Contains(plain, "if (a < b) {}");
        var html = FeedbackCopyFormatter.Format(draft, "HTML");
        StringAssert.Contains(html, "<h2>Review</h2>");
        StringAssert.Contains(html, "<strong>Good work</strong>");
        StringAssert.Contains(html, "a &lt; b");
        Assert.IsFalse(html.Contains("<script>"));
    }
}
