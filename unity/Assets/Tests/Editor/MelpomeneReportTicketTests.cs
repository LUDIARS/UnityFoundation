using Foundation.Melpomene;
using NUnit.Framework;

namespace Foundation.Tests.Editor
{
    public class MelpomeneReportTicketTests
    {
        [Test]
        public void GenerateIssueTitle_PrefixesMelpomene()
        {
            var ticket = new MelpomeneReportTicket { title = "落下バグ" };
            Assert.AreEqual("[Melpomene] 落下バグ", ticket.GenerateIssueTitle());
        }

        [Test]
        public void GenerateIssueBody_ContainsCoreFields()
        {
            var ticket = new MelpomeneReportTicket
            {
                userName = "tester",
                title = "落下バグ",
                description = "床を貫通する",
                sceneName = "StageA",
                platform = "WindowsPlayer",
                priority = MelpomeneRuntimePriority.High,
                category = MelpomeneRuntimeCategory.Bug,
            };

            var body = ticket.GenerateIssueBody();

            Assert.That(body, Does.Contain("StageA"));
            Assert.That(body, Does.Contain("WindowsPlayer"));
            Assert.That(body, Does.Contain("床を貫通する"));
            Assert.That(body, Does.Contain("High"));
            Assert.That(body, Does.Contain("Bug"));
        }

        [Test]
        public void GenerateIssueBody_FallsBackToAnonymous_WhenUserNameEmpty()
        {
            var ticket = new MelpomeneReportTicket { userName = "", description = "x" };
            Assert.That(ticket.GenerateIssueBody(), Does.Contain("(匿名)"));
        }
    }
}
