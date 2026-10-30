using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Models;
using Voat.Tests.Infrastructure;

namespace Voat.Tests.Repository
{
    /// <summary>
    /// The admin dashboard's numbers and the user-lookup search. Both read the
    /// users connection (CONSTANTS.CONNECTION_USERS) as well as the content
    /// one, which is the easy thing to get wrong here — AspNetUsers does not
    /// exist on the content connection and fails at runtime, not compile time.
    /// </summary>
    [TestClass]
    public class AdminStatsRepositoryTests : BaseUnitTest
    {
        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.AdminStats")]
        public async Task GetSitewideStats_ReturnsNonNegativeCounts()
        {
            var user = TestHelper.SetPrincipal("TestUser01");

            SitewideStats stats;
            using (var repo = new Voat.Data.Repository(user))
            {
                stats = await repo.GetSitewideStats();
            }

            Assert.IsNotNull(stats, "The dashboard must always get a stats object, even on an empty site.");
            Assert.IsTrue(stats.TotalUsers >= 0, "User count can never be negative.");
            Assert.IsTrue(stats.NewUsersLastSevenDays >= 0, "Seven-day signups can never be negative.");
            Assert.IsTrue(stats.TotalSubmissions >= 0, "Submission count can never be negative.");
            Assert.IsTrue(stats.TotalComments >= 0, "Comment count can never be negative.");
            Assert.IsTrue(stats.TotalSubverses >= 0, "Subverse count can never be negative.");
            Assert.IsTrue(stats.NewUsersLastSevenDays <= stats.TotalUsers,
                "Seven-day signups are a subset of all users and must not exceed the total.");
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.AdminStats")]
        public async Task SearchUsers_PrefixTerm_FindsSeededUser()
        {
            var user = TestHelper.SetPrincipal("TestUser01");

            //Seeded by TestDataInitializer.CreateUser.
            string name = USERNAMES.User500CCP;

            using (var repo = new Voat.Data.Repository(user))
            {
                var results = await repo.SearchUsers(name);

                Assert.IsTrue(results.Any(x => x.UserName == name),
                    $"A prefix of a seeded username must find that account; searching for '{name}'.");
                Assert.IsTrue(results.All(x => x.UserName.StartsWith(name, StringComparison.OrdinalIgnoreCase)),
                    "Every hit must actually start with the searched prefix.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.AdminStats")]
        public async Task SearchUsers_GuidTerm_ReturnsEmpty()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            //No account is named this, so any hit would be the LIKE pattern
            //escaping (or the prefix logic) doing the wrong thing.
            string term = Guid.NewGuid().ToString("N");

            using (var repo = new Voat.Data.Repository(user))
            {
                var results = await repo.SearchUsers(term);

                Assert.IsFalse(results.Any(), $"A term that matches no account must return nothing; searching for '{term}'.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.AdminStats")]
        public async Task SearchUsers_EmptyTerm_ReturnsEmpty()
        {
            var user = TestHelper.SetPrincipal("TestUser01");

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsFalse((await repo.SearchUsers(null)).Any(), "A null term must not turn into a match-everything query.");
                Assert.IsFalse((await repo.SearchUsers("")).Any(), "An empty term must not turn into a match-everything query.");
                Assert.IsFalse((await repo.SearchUsers("   ")).Any(), "A whitespace term must not turn into a match-everything query.");
            }
        }
    }
}
