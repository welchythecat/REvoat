using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Command;
using Voat.Domain.Models;
using Voat.Tests.Infrastructure;

namespace Voat.Tests.Repository
{
    /// <summary>
    /// UnbanGlobally is the other half of BanGlobally — before it existed a ban
    /// was permanent short of editing the database by hand. These cover the
    /// delete itself; GlobalUnbanCommandTests covers the authorization and
    /// cache eviction layered on top.
    /// </summary>
    [TestClass]
    public class GlobalUnbanRepositoryTests : BaseUnitTest
    {
        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.GlobalUnban")]
        public async Task UnbanGlobally_ExistingUserBan_RemovesRow()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            //GUID suffix keeps this from colliding with a ban left by an
            //earlier run or with a real account name.
            string name = "unbanuser" + Guid.NewGuid().ToString("N").Substring(0, 12);

            var banList = new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } };

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.BanGlobally(banList, "unit test ban");
                Assert.IsNotNull(repo.UserBan(name), "A ban that was just written must be readable before it can be removed.");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var response = await repo.UnbanGlobally(banList);
                Assert.AreEqual(Status.Success, response.Status, response.Message);
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsNull(repo.UserBan(name), "UnbanGlobally must delete the row so enforcement stops seeing the ban.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.GlobalUnban")]
        public async Task UnbanGlobally_ExistingDomainBan_RemovesRow()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            string domain = "unban" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".test";

            var banList = new[] { new GenericReference<BanType>() { Name = domain, Type = BanType.Domain } };

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.BanGlobally(banList, "unit test ban");
                Assert.IsTrue(repo.GetBannedDomains().Any(x => x.Domain == domain), "A domain ban that was just written must be listed.");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var response = await repo.UnbanGlobally(banList);
                Assert.AreEqual(Status.Success, response.Status, response.Message);
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsFalse(repo.GetBannedDomains().Any(x => x.Domain == domain), "UnbanGlobally must delete the domain row, not just stop returning it somewhere.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.GlobalUnban")]
        public async Task UnbanGlobally_MissingBan_IsIdempotentSuccess()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            //Never banned. Two admins racing on the same row must not produce
            //a failure for whoever finishes second.
            string name = "unbanuser" + Guid.NewGuid().ToString("N").Substring(0, 12);

            var banList = new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } };

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsNull(repo.UserBan(name), "The fixture must start unbanned for this to mean anything.");

                var response = await repo.UnbanGlobally(banList);

                Assert.AreEqual(Status.Success, response.Status, "Removing a ban that is already gone must still report success so the second admin's click is not an error.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.GlobalUnban")]
        public async Task UnbanGlobally_UserCaseIsIgnored()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            //Usernames are stored in original case and matched case-insensitively,
            //so the admin typing a different casing must still hit the row.
            string name = "UnbanUser" + Guid.NewGuid().ToString("N").Substring(0, 12);

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.BanGlobally(new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } }, "unit test ban");
                Assert.IsNotNull(repo.UserBan(name), "The fixture ban must exist before the case-insensitive removal is tested.");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var response = await repo.UnbanGlobally(new[] { new GenericReference<BanType>() { Name = name.ToLowerInvariant(), Type = BanType.User } });
                Assert.AreEqual(Status.Success, response.Status, response.Message);
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsNull(repo.UserBan(name), "A differently cased spelling of the same name must still remove the ban.");
            }
        }
    }
}
