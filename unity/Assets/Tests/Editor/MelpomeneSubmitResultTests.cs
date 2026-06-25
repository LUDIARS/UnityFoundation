using Foundation.Melpomene;
using NUnit.Framework;

namespace Foundation.Tests.Editor
{
    public class MelpomeneSubmitResultTests
    {
        [Test]
        public void Fail_SetsErrorAndNotSuccess()
        {
            var result = MelpomeneSubmitResult.Fail("boom");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("boom", result.Error);
        }

        [Test]
        public void Ok_SetsNumberUrlAndSuccess()
        {
            var result = MelpomeneSubmitResult.Ok(123, "https://example.com/issues/123");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(123, result.IssueNumber);
            Assert.AreEqual("https://example.com/issues/123", result.IssueUrl);
            Assert.IsNull(result.Error);
        }
    }
}
