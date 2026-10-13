using System.IO;
using GuidedGrade.Services;
using GuidedGrade.Models;
using GuidedGrade.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class CommentPlacementTests
{
    [TestMethod]
    public void CrossFileMoveIsAtomicAndPreservesApprovalAndPoints()
    {
        var database=Path.GetTempFileName();
        try
        {
            var store=new CommentPersistenceService(database);
            var original=new SectionFeedback { SectionName="Finding", StartLine=2, EndLine=2, ReviewContext="student/lab", SuggestedScore=14, ReviewStatus=FeedbackReviewStatus.Approved };
            store.SaveComments("header.h",[original]);
            var edited=store.LoadComments("header.h").Single(); edited.StartLine=edited.EndLine=7;
            store.MoveComment("header.h","source.cpp",original,edited);
            Assert.AreEqual(0,store.LoadComments("header.h").Count);
            var moved=store.LoadComments("source.cpp").Single();
            Assert.AreEqual(7,moved.StartLine); Assert.AreEqual(14d,moved.SuggestedScore); Assert.AreEqual(FeedbackReviewStatus.Approved,moved.ReviewStatus);
            store.SaveComments("header.h",[original]);
            Assert.ThrowsException<Microsoft.Data.Sqlite.SqliteException>(()=>store.MoveComment("header.h","source.cpp",original,edited));
            Assert.AreEqual(1,store.LoadComments("header.h").Count);
            Assert.AreEqual(1,store.LoadComments("source.cpp").Count);
            original.IsPinned=true; store.SaveComments("header.h",[original]);
            Assert.ThrowsException<InvalidOperationException>(()=>store.MoveComment("header.h","other.cpp",original,edited));
            Assert.AreEqual(1,store.LoadComments("header.h").Count);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(database); }
    }
    [TestMethod]
    public void FileSelectionValidatesDestinationLineAndPinnedFile()
    {
        var comment=new SectionFeedback { Explanation="Comment",StartLine=2,EndLine=2 };
        var edit=new InlineReviewEditViewModel(comment,10,15,["source.cpp","header.h"],path=>path=="header.h"?3:10);
        edit.FileIndex.Value=1; edit.Line.Value="4";
        Assert.IsFalse(edit.Apply()); Assert.AreEqual(2,comment.StartLine);
        edit.Line.Value="3"; Assert.IsTrue(edit.Apply()); Assert.AreEqual("header.h",edit.DestinationFile);
        comment.IsPinned=true; Assert.IsFalse(edit.Apply());
    }
    [TestMethod]
    public void DropMovesAnchorAndConfirmsLocationWithoutChangingApprovalOrPoints()
    {
        var comment=new SectionFeedback { StartLine=2, EndLine=4, SuggestedScore=12, RubricReview=new() { LocationResolved=false } };
        Assert.IsTrue(CommentPlacementService.Move(comment,8,10));
        Assert.AreEqual(8,comment.StartLine); Assert.AreEqual(10,comment.EndLine);
        Assert.IsTrue(comment.RubricReview.LocationResolved); Assert.AreEqual(12d,comment.SuggestedScore);
        Assert.AreEqual(FeedbackReviewStatus.Pending,comment.ReviewStatus);
        Assert.IsFalse(CommentPlacementService.Move(comment,11,10)); Assert.AreEqual(8,comment.StartLine);
    }
    [TestMethod]
    public void PinLocksLocationUntilUnpinnedAndStillAllowsTextAndPointsEdits()
    {
        var comment=new SectionFeedback { StartLine=3, EndLine=3, Explanation="Comment", RubricReview=new() { MaximumPoints=15 } };
        CommentPlacementService.TogglePin(comment);
        Assert.IsFalse(CommentPlacementService.Move(comment,7,10));
        var edit=new InlineReviewEditViewModel(comment,10,15);
        Assert.IsFalse(edit.CanMove); edit.Comment.Value="Your revised comment"; edit.Points.Value="14";
        Assert.IsTrue(edit.Apply()); Assert.AreEqual(14d,comment.SuggestedScore);
        edit.Line.Value="7"; Assert.IsFalse(edit.Apply()); Assert.AreEqual(3,comment.StartLine);
        CommentPlacementService.TogglePin(comment); Assert.IsTrue(CommentPlacementService.Move(comment,7,10));
    }
    [TestMethod]
    public void PinAndMovedLineSurviveReopeningStorage()
    {
        var path=Path.GetTempFileName();
        try
        {
            var store=new CommentPersistenceService(path);
            var comment=new SectionFeedback { SectionName="Finding", StartLine=1, EndLine=1, ReviewContext="student/lab" };
            Assert.IsTrue(CommentPlacementService.Move(comment,9,20)); CommentPlacementService.TogglePin(comment);
            store.SaveComments("source.cpp",[comment]);
            var restored=new CommentPersistenceService(path).LoadComments("source.cpp").Single();
            Assert.AreEqual(9,restored.StartLine); Assert.IsTrue(restored.IsPinned);
            Assert.IsFalse(CommentPlacementService.Move(restored,4,20));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
}
