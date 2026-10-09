using System.IO;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class ReviewWorkspaceServiceTests
{
    [TestMethod]
    public void UnloadedCacheCannotDeletePersistedReviewsAndTrackPreservesOtherContexts()
    {
        WithStore((store, root) =>
        {
            var path=Path.Combine(root,"Game.cpp");
            store.SaveComments(path,[new() { SectionName="Earlier",ReviewContext="lab1" }]);
            var service=new ReviewWorkspaceService(store,()=>path);
            Assert.IsFalse(service.SaveCached(path));
            service.Track(path,new() { SectionName="Current",ReviewContext="lab2" });
            Assert.IsTrue(service.SaveCached(path));
            Assert.AreEqual(2,store.LoadComments(path).Count);
            store.SaveComments(path,[new() { SectionName="External write" }]);
            Assert.AreEqual("External write",service.Reload(path).Single().SectionName);
        });
    }

    [TestMethod]
    public void CompletionRoutesToEvidenceFileAndPreservesPinnedDecisions()
    {
        WithStore((store, root) =>
        {
            var header=Path.Combine(root,"Game.h"); var source=Path.Combine(root,"Game.cpp");
            File.WriteAllText(header,"class Game {};\n"); File.WriteAllText(source,"// implementation\nvoid Run() {}\n");
            var pinned=new SectionFeedback { SectionName="Pinned",ReviewContext="lab",StartLine=1,EndLine=1,IsPinned=true,
                ReviewStatus=FeedbackReviewStatus.Approved,SuggestedScore=14,
                RubricReview=new() { CriterionId=1,MaximumPoints=15,ReportId="old",LocationResolved=true } };
            store.SaveComments(header,[pinned]);
            var generation=new ReviewGeneration();
            var targets=new[] { (header,generation.Capture(header)),(source,generation.Capture(source)) };
            var service=new ReviewWorkspaceService(store,()=>source);
            var result=service.CompleteInline(targets,"lab",[
                new(1,"Replacement",15,0,true,1,"class Game {};","Replace"),
                new(2,"Creating Methods",15,12,true,1,"void Run() {}","Correct method")
            ],true,generation);
            Assert.AreEqual(2,result.Paths.Length); Assert.IsTrue(result.UpdateGrade);
            Assert.AreEqual("Pinned",store.LoadComments(header).Single().SectionName);
            var moved=store.LoadComments(source).Single(); Assert.AreEqual(2,moved.StartLine);
            Assert.AreEqual(FeedbackReviewStatus.Approved,moved.ReviewStatus);
            Assert.AreEqual(new StudentGrade(26,30),service.CalculateGrade("lab"));
            moved.ReviewStatus=FeedbackReviewStatus.Rejected; store.SaveComments(source,[moved]);
            Assert.AreEqual(new StudentGrade(14,30),service.CalculateGrade("lab"));
            Assert.IsNull(service.CalculateGrade("other lab"));
        });
    }

    [TestMethod]
    public void ClearedCaptureStopsCompletionBeforeReadingOrWritingFiles()
    {
        WithStore((store, root) =>
        {
            var path=Path.Combine(root,"missing.cpp"); var generations=new ReviewGeneration();
            var capture=generations.Capture(path); generations.Clear(path);
            var service=new ReviewWorkspaceService(store,()=>null);
            Assert.AreEqual(0,service.CompleteInline([(path,capture)],"lab",[],false,generations).Paths.Length);
            Assert.AreEqual(0,store.GetAllReviewedPaths().Count);
        });
    }

    [TestMethod]
    public void GradeEditorRejectsInvalidPointsAndKeepsPersistenceFailureVisible()
    {
        StudentGrade? saved=null;
        var model=new StudentGradeEditViewModel("Student","Assignment",null,100,grade=>saved=grade);
        model.Earned.Value="101"; Assert.IsFalse(model.Save(false)); Assert.IsNull(saved);
        model.Earned.Value="NaN"; Assert.IsFalse(model.Save(false));
        model.Earned.Value="90"; Assert.IsTrue(model.Save(false)); Assert.AreEqual(new StudentGrade(90,100),saved);
        Assert.IsTrue(model.Save(true)); Assert.IsNull(saved);
        var failed=new StudentGradeEditViewModel("Student","Assignment",new(90,100),100,_=>throw new IOException("database unavailable"));
        Assert.IsFalse(failed.Save(false)); StringAssert.Contains(failed.Error.Value,"database unavailable");
    }

    private static void WithStore(Action<CommentPersistenceService,string> test)
    {
        var root=Path.Combine(Path.GetTempPath(),"review-workspace-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(new CommentPersistenceService(Path.Combine(root,"comments.db")),root); }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root,true); }
    }
}
