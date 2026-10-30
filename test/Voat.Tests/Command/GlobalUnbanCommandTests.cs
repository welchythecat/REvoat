using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Command;
using Voat.Domain.Models;
using Voat.Domain.Query;
using Voat.Tests.Infrastructure;

namespace Voat.Tests.CommandTests
{
    /// <summary>
    /// GlobalUnbanCommand is the role-gated wrapper over
    /// Repository.UnbanGlobally. Authorization is doubled here because
    /// [Authorize] on Repository methods is decorative — no filter pipeline
    /// ever runs it, so the command stage is the real gate.
    /// </summary>
    [TestClass]
    public class GlobalUnbanCommandTests : BaseCommandTest
    {
        [TestMethod]
        [TestCategory("Command"), TestCategory("Command.GlobalUnban")]
        public async Task GlobalUnbanCommand_WithoutRole_IsDenied()
        {
            //Authenticated but holds no admin role.
            var user = TestHelper.SetPrincipal("UnitTestUser01");

            string name = "unbanuser" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var cmd = new GlobalUnbanCommand(new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } })
                .SetUserContext(user);

            var response = await cmd.Execute();

            Assert.AreEqual(Status.Denied, response.Status, "An account without GlobalAdmin/Admin/DelegateAdmin/GlobalBans must not be able to lift a ban.");
        }

        [TestMethod]
        [TestCategory("Command"), TestCategory("Command.GlobalUnban")]
        public async Task GlobalUnbanCommand_EmptyList_IsInvalid()
        {
            var user = TestHelper.SetPrincipal("UnitTestUser01", "GlobalBans");

            var cmd = new GlobalUnbanCommand(new GenericReference<BanType>[0]).SetUserContext(user);
            var response = await cmd.Execute();

            Assert.AreEqual(Status.Invalid, response.Status, "An empty ban list is a bad request, not a silent success.");
        }

        [TestMethod]
        [TestCategory("Command"), TestCategory("Command.GlobalUnban")]
        public async Task GlobalUnbanCommand_WithGlobalBansRole_Succeeds()
        {
            var user = TestHelper.SetPrincipal("UnitTestUser01", "GlobalBans");
            string name = "unbanuser" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var banList = new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } };

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.BanGlobally(banList, "unit test ban");
            }

            var cmd = new GlobalUnbanCommand(banList).SetUserContext(user);
            var response = await cmd.Execute();

            Assert.AreEqual(Status.Success, response.Status, response.Message);

            using (var repo = new Voat.Data.Repository(user))
            {
                Assert.IsNull(repo.UserBan(name), "A successful unban command must leave no row behind.");
            }
        }

        [TestMethod]
        [TestCategory("Command"), TestCategory("Command.GlobalUnban")]
        public async Task GlobalUnbanCommand_Success_EvictsBannedUsersCache()
        {
            //The cache eviction is half the unban: QueryBannedUsers is cached
            //for an hour, so without eviction enforcement (GlobalBanRule,
            //UserHelper.IsUserGloballyBanned, messaging, report submission)
            //would keep rejecting the account long after the row was gone.
            var user = TestHelper.SetPrincipal("UnitTestUser01", "GlobalBans");
            string name = "unbanuser" + Guid.NewGuid().ToString("N").Substring(0, 12);
            var banList = new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } };

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.BanGlobally(banList, "unit test ban");
            }

            //Prime the cache while the ban is in place, so the second read can
            //only come back clean if the unban evicted the key.
            var before = await new QueryBannedUsers().SetUserContext(user).ExecuteAsync();
            Assert.IsTrue(before.Any(x => x.UserName == name), "The primed cache must contain the ban for this test to prove anything.");

            var cmd = new GlobalUnbanCommand(banList).SetUserContext(user);
            var response = await cmd.Execute();
            Assert.AreEqual(Status.Success, response.Status, response.Message);

            var after = await new QueryBannedUsers().SetUserContext(user).ExecuteAsync();
            Assert.IsFalse(after.Any(x => x.UserName == name), "The banned-users cache key must be evicted on success, otherwise the unban is invisible for up to an hour.");
        }
    }
}
