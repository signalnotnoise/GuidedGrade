using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class NativeExitCodesTests
{
    [TestMethod]
    public void Describe_ExplainsDllNotFound()
    {
        var text = NativeExitCodes.Describe(unchecked((int)0xC0000135));
        StringAssert.Contains(text, "STATUS_DLL_NOT_FOUND");
        Assert.IsTrue(NativeExitCodes.IsDllNotFound(-1073741515));
    }

    [TestMethod]
    public void Describe_HandlesNull()
    {
        Assert.AreEqual("n/a", NativeExitCodes.Describe(null));
    }

    [TestMethod]
    public void IsCrash_DetectsAccessViolation()
    {
        Assert.IsTrue(NativeExitCodes.IsCrash(unchecked((int)0xC0000005)));
        Assert.IsFalse(NativeExitCodes.IsCrash(0));
        Assert.IsFalse(NativeExitCodes.IsCrash(null));
    }
}
