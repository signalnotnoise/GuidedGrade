using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class ViolationsMatcherTests
{
    // -------------------------------------------------------------------------
    // Core word-boundary matching
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CountMatches_ExactKeyword_IsDetected()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        Assert.AreEqual(1, matcher.CountMatches("auto x = 5;"));
    }

    [TestMethod]
    public void CountMatches_KeywordPartOfLongerWord_IsNotDetected()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        Assert.AreEqual(0, matcher.CountMatches("automatic"));
    }

    [TestMethod]
    public void CountMatches_MultipleKeywordOccurrences_AllCounted()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        Assert.AreEqual(2, matcher.CountMatches("auto x = 0;\nauto y = 1;"));
    }

    [TestMethod]
    public void CountMatches_MultipleDistinctKeywords_AllCounted()
    {
        var matcher = new ViolationsMatcher(["try", "catch"]);
        Assert.AreEqual(2, matcher.CountMatches("try { } catch (...) { }"));
    }

    // -------------------------------------------------------------------------
    // Method-call pattern  (e.g. "resize()" matches any call to resize)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CountMatches_MethodPattern_MatchesNoArgCall()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        Assert.AreEqual(1, matcher.CountMatches("v.resize();"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_MatchesSingleArgCall()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        Assert.AreEqual(1, matcher.CountMatches("v.resize(10);"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_MatchesMultiArgCall()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        Assert.AreEqual(1, matcher.CountMatches("v.resize(n, 0);"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_DoesNotMatchDifferentMethodWithSamePrefix()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        // my_resize is a different identifier
        Assert.AreEqual(0, matcher.CountMatches("v.my_resize(10);"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_IsCaseSensitive()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        Assert.AreEqual(0, matcher.CountMatches("v.Resize(10);"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_CountsMultipleCalls()
    {
        var matcher = new ViolationsMatcher(["resize()"]);
        Assert.AreEqual(2, matcher.CountMatches("a.resize(5); b.resize(n);"));
    }

    [TestMethod]
    public void CountMatches_MethodPattern_IgnoreAndClear()
    {
        var matcher = new ViolationsMatcher(["ignore()", "clear()"]);
        Assert.AreEqual(2, matcher.CountMatches("cin.ignore(100, '\\n'); v.clear();"));
    }

    // -------------------------------------------------------------------------
    // Comment stripping — single-line (//)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CountMatches_KeywordInSingleLineComment_IsNotDetected()
    {
        var matcher = new ViolationsMatcher(["try"]);
        Assert.AreEqual(0, matcher.CountMatches("// try this approach"));
    }

    [TestMethod]
    public void CountMatches_KeywordAfterInlineComment_IsNotDetected()
    {
        var matcher = new ViolationsMatcher(["try"]);
        Assert.AreEqual(0, matcher.CountMatches("int x = 0; // try again"));
    }

    [TestMethod]
    public void CountMatches_KeywordInCodeBeforeComment_IsDetected()
    {
        var matcher = new ViolationsMatcher(["try"]);
        // "try" is in real code; the comment does not contain it
        Assert.AreEqual(1, matcher.CountMatches("try { } // catch errors"));
    }

    [TestMethod]
    public void CountMatches_KeywordOnlyInComment_ZeroMatches()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        Assert.AreEqual(0, matcher.CountMatches("// auto is a C++ keyword"));
    }

    // -------------------------------------------------------------------------
    // Comment stripping — block comments (/* */)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CountMatches_KeywordInBlockComment_IsNotDetected()
    {
        var matcher = new ViolationsMatcher(["try"]);
        Assert.AreEqual(0, matcher.CountMatches("/* try to avoid exceptions */"));
    }

    [TestMethod]
    public void CountMatches_KeywordInBlockCommentOnlyOneOfTwo_CountsOne()
    {
        var matcher = new ViolationsMatcher(["try"]);
        // First "try" is in the block comment; second is live code
        Assert.AreEqual(1, matcher.CountMatches("/* try */ try { }"));
    }

    [TestMethod]
    public void CountMatches_KeywordInMultilineBlockComment_IsNotDetected()
    {
        var matcher = new ViolationsMatcher(["try"]);
        string code = "/*\n * try this\n */\ntry { }";
        Assert.AreEqual(1, matcher.CountMatches(code));
    }

    // -------------------------------------------------------------------------
    // GetViolations — line number accuracy
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetViolations_ReturnsCorrectLineNumber()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        string code = "int x = 0;\nauto y = 1;";
        var violations = matcher.GetViolations(code);
        Assert.AreEqual(1, violations.Count);
        Assert.AreEqual(2, violations[0].LineNumber);
    }

    [TestMethod]
    public void GetViolations_CommentedKeyword_ReturnsNoViolations()
    {
        var matcher = new ViolationsMatcher(["auto"]);
        var violations = matcher.GetViolations("// auto x = 0;");
        Assert.AreEqual(0, violations.Count);
    }
}
