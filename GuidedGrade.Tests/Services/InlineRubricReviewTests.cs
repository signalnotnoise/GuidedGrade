using System.IO;
using System.Text.Json;
using GuidedGrade.Services;
using GuidedGrade.Models;
using GuidedGrade.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class InlineRubricReviewTests
{
    private static ReviewFindingEnvelope.FindingDraft Finding(bool verified = false) => new(1,"Part B-1",15,8,verified,1,"BuildBoard();","Your board needs review.");
    [TestMethod]
    public void LocatesQuoteInActualFileAtActualLine()
    {
        var location = InlineRubricReview.Locate(Finding(), ["void Run();", "// header\n\nBuildBoard();"]);
        Assert.AreEqual((2,3), location);
    }
    [TestMethod]
    public void InvalidQuoteUsesTodoSectionOrRemainsExplicitlyUnlocated()
    {
        var finding = Finding() with { Quote = "made up" };
        Assert.AreEqual((2,2), InlineRubricReview.Locate(finding, ["", "// header\n// TODO: B-1 BuildBoard"]));
        Assert.AreEqual((1,0), InlineRubricReview.Locate(finding, ["void Run();"]));
    }
    [TestMethod]
    public void UnverifiedFindingsNeverAutoApprove()
    {
        var pending = InlineRubricReview.Create(Finding(),2,"student-lab","report",true);
        Assert.AreEqual(FeedbackReviewStatus.Pending, pending.ReviewStatus);
        var approved = InlineRubricReview.Create(Finding(true),2,"student-lab","report",true);
        Assert.AreEqual(FeedbackReviewStatus.Approved, approved.ReviewStatus);
        Assert.AreEqual(FeedbackReviewStatus.Pending, InlineRubricReview.Create(Finding(true),0,"student-lab","report",true).ReviewStatus);
    }
    [TestMethod]
    public void ModifyEditsCommentPointsAndLocationWithoutApproving()
    {
        var comment = InlineRubricReview.Create(Finding(),0,"context","report",false);
        var model = new InlineReviewEditViewModel(comment,20,15);
        model.Comment.Value = "You implemented this correctly."; model.Points.Value = "15"; model.Line.Value = "8";
        Assert.IsTrue(model.Apply()); Assert.AreEqual(15d,comment.SuggestedScore); Assert.AreEqual(8,comment.StartLine);
        Assert.AreEqual("You implemented this correctly.",comment.Explanation);
        Assert.IsTrue(comment.RubricReview!.LocationResolved); Assert.AreEqual(FeedbackReviewStatus.Pending,comment.ReviewStatus);
        model.Points.Value = "16"; Assert.IsFalse(model.Apply()); Assert.AreEqual(15d,comment.SuggestedScore);
    }
    [TestMethod]
    public void ApprovalScoreExcludesPendingRejectedAndDuplicateCriteria()
    {
        var approved = InlineRubricReview.Create(Finding(true),2,"context","report",true);
        var pending = InlineRubricReview.Create(Finding() with { Id=2 },2,"context","report",false);
        var rejected = InlineRubricReview.Create(Finding() with { Id=3 },2,"context","report",false);
        rejected.ReviewStatus=FeedbackReviewStatus.Rejected;
        Assert.AreEqual(8d,InlineRubricReview.ApprovedPoints([approved,approved,pending,rejected]));
    }
    [TestMethod]
    public void InlineMetadataAndInstructorEditsSurviveDatabaseReload()
    {
        var path=Path.GetTempFileName();
        try
        {
            var store=new CommentPersistenceService(path);
            var comment=InlineRubricReview.Create(Finding(),7,"student/lab","report",false);
            comment.Explanation="Your instructor comment"; comment.SuggestedScore=12; comment.ReviewStatus=FeedbackReviewStatus.Approved;
            store.SaveComments("source.cpp",[comment]);
            var saved=store.LoadComments("source.cpp").Single();
            Assert.AreEqual(12d,saved.SuggestedScore); Assert.AreEqual(7,saved.StartLine); Assert.AreEqual(1,saved.RubricReview!.CriterionId);
            Assert.AreEqual(15d,saved.RubricReview.MaximumPoints); Assert.AreEqual("Your instructor comment",saved.Explanation);
            Assert.AreEqual(FeedbackReviewStatus.Approved,saved.ReviewStatus);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [TestMethod]
    public void PacketPreservesAllCriteriaAndOverallCommentWhileRemovingInternalMarkup()
    {
        var assignment=new GradingAssignment { Rubric=[new("Part B-1",15),new("Part B-2",15)] };
        var reply=JsonSerializer.Serialize(new OverallReviewResult.Response { Criteria=[new() {Id=1,File=1,Quote="BuildBoard();",Status="verified",Earned=15,Reason="Good"}], Feedback="Your overall comment." });
        var packet=ReviewFindingEnvelope.Unpack(ReviewFindingEnvelope.Pack("Normal report",reply,assignment,[new() {Name="file-1.cpp",Content="BuildBoard();"}]));
        Assert.AreEqual("Normal report",packet.Text); Assert.AreEqual(3,packet.Findings.Count);
        Assert.IsFalse(packet.Findings.Single(f=>f.Id==2).Verified);
        Assert.AreEqual("Your overall comment.",packet.Findings.Single(f=>f.Id==0).Reason);
    }
    [TestMethod]
    public void ApprovedDeductionsSubtractOnceAndConfirmationPreventsAutoApproval()
    {
        var earned=InlineRubricReview.Create(Finding(true) with { Earned=15 },2,"context","report",true);
        var deduction=InlineRubricReview.Create(Finding(true) with { IsDeduction=true, Maximum=5, Earned=5, RequiresInstructorConfirmation=true },2,"context","report",true);
        Assert.AreEqual(FeedbackReviewStatus.Pending,deduction.ReviewStatus);
        deduction.ReviewStatus=FeedbackReviewStatus.Approved;
        Assert.AreEqual(10d,InlineRubricReview.ApprovedPoints([earned,deduction,deduction]));
    }
    [TestMethod]
    public void IdenticalRubricNamesAndLocationsPersistAsSeparateCriteria()
    {
        var path=Path.GetTempFileName();
        try
        {
            var store=new CommentPersistenceService(path);
            var first=InlineRubricReview.Create(Finding(),2,"context","report",false);
            var second=InlineRubricReview.Create(Finding() with { Id=2 },2,"context","report",false);
            store.SaveComments("source.cpp",[first,second]);
            Assert.AreEqual(2,store.LoadComments("source.cpp").Count);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

}
