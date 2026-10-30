#region LICENSE

/*

    Copyright(c) Voat, Inc.

    This file is part of Voat.

    This source file is subject to version 3 of the GPL license,
    that is bundled with this package in the file LICENSE, and is
    available online at http://www.gnu.org/licenses/gpl-3.0.txt;
    you may not use this file except in compliance with the License.

    Software distributed under the License is distributed on an
    "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either express
    or implied. See the License for the specific language governing
    rights and limitations under the License.

    All Rights Reserved.

*/

#endregion LICENSE

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;
using Voat.Configuration;
using Voat.Data;
using Voat.Tests.Infrastructure;

namespace Voat.Tests.Repository
{
    /// <summary>
    /// SiteSetting backs the owner-editable site settings. Two properties matter:
    /// a key that has never been written must read as null (so the caller falls
    /// back to appsettings.json rather than a value of its own), and a second
    /// save must update the same row instead of adding another.
    ///
    /// The gate keys are fixed site-wide names rather than per-test ones, so the
    /// policy tests clear them first instead of relying on execution order.
    /// </summary>
    [TestClass]
    public class SiteSettingRepositoryTests : BaseUnitTest
    {
        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_SaveThenGet_RoundTrips()
        {
            var user = TestHelper.SetPrincipal("TestUser01");

            //A GUID keeps this from colliding with a row from an earlier run.
            string name = "tests.setting." + Guid.NewGuid().ToString("N").Substring(0, 12);

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(name, "a stored value", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var value = repo.GetSiteSetting(name);

                Assert.AreEqual("a stored value", value, "A value that was just saved must come back.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public void SiteSetting_GetUnset_ReturnsNull()
        {
            var user = TestHelper.SetPrincipal("TestUser01");

            //A key that has never been written.
            string name = "tests.setting." + Guid.NewGuid().ToString("N").Substring(0, 12);

            using (var repo = new Voat.Data.Repository(user))
            {
                var value = repo.GetSiteSetting(name);

                Assert.IsNull(value, "An unknown key must return null — that is the caller's fall-back-to-config signal.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_SecondSave_UpdatesInPlace()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            string name = "tests.setting." + Guid.NewGuid().ToString("N").Substring(0, 12);

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(name, "first", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(name, "second", "TestUser02");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var value = repo.GetSiteSetting(name);

                Assert.AreEqual("second", value, "A later save must win.");
            }

            int rowCount;
            using (var db = new Voat.Data.Models.VoatDataContext())
            {
                rowCount = db.SiteSetting.Count(x => x.Name == name);
            }

            Assert.AreEqual(1, rowCount, "A second save must update the existing row rather than insert a second one.");
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public void SiteSetting_GetSubverseCreationPolicy_NoRows_FallsBackToConfig()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsTrue(policy.AccountAgeEnabled, "With no rows stored the age gate must stay on — that is the behaviour every existing install has today.");
                Assert.IsTrue(policy.CommentPointsEnabled, "With no rows stored the CCP gate must stay on.");
                Assert.IsTrue(policy.SubmissionPointsEnabled, "With no rows stored the SCP gate must stay on.");

                Assert.AreEqual(VoatSettings.Instance.MinimumAccountAgeInDaysForSubverseCreation, policy.MinimumAccountAgeDays,
                    "With no rows stored the required age must come from appsettings.json, not from a value of its own.");
                Assert.AreEqual(VoatSettings.Instance.MinimumCommentPointsForSubverseCreation, policy.MinimumCommentPoints,
                    "With no rows stored the CCP threshold must come from appsettings.json.");
                Assert.AreEqual(VoatSettings.Instance.MinimumSubmissionPointsForSubverseCreation, policy.MinimumSubmissionPoints,
                    "With no rows stored the SCP threshold must come from appsettings.json.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_GetSubverseCreationPolicy_AgeGateDisabled_SkipsAgeGate()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED, "false", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsFalse(policy.AccountAgeEnabled, "A stored \"false\" must turn the age gate off.");
                Assert.IsTrue(policy.CommentPointsEnabled, "Switching the age gate off must leave the CCP gate alone.");
                Assert.IsTrue(policy.SubmissionPointsEnabled, "Switching the age gate off must leave the SCP gate alone.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_GetSubverseCreationPolicy_AgeGate_UsesStoredDays()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED, "true", "TestUser01");
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS, "5", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsTrue(policy.AccountAgeEnabled, "A stored \"true\" must leave the age gate on.");
                Assert.AreEqual(5, policy.MinimumAccountAgeDays, "A stored day count must be the one that is enforced.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_GetSubverseCreationPolicy_CommentPointsGate_UsesStoredPoints()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED, "true", "TestUser01");
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS, "42", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsTrue(policy.CommentPointsEnabled, "A stored \"true\" must leave the CCP gate on.");
                Assert.AreEqual(42, policy.MinimumCommentPoints, "A stored CCP threshold must be the one that is enforced.");
                Assert.AreEqual(VoatSettings.Instance.MinimumAccountAgeInDaysForSubverseCreation, policy.MinimumAccountAgeDays,
                    "Changing the CCP threshold must not disturb the age gate's fall back to config.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_GetSubverseCreationPolicy_SubmissionPointsGate_UsesStoredPoints()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED, "true", "TestUser01");
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS, "77", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsTrue(policy.SubmissionPointsEnabled, "A stored \"true\" must leave the SCP gate on.");
                Assert.AreEqual(77, policy.MinimumSubmissionPoints, "A stored SCP threshold must be the one that is enforced.");
            }
        }

        [TestMethod]
        [TestCategory("Repository"), TestCategory("Repository.SiteSetting")]
        public async Task SiteSetting_GetSubverseCreationPolicy_AllGatesDisabled_TurnsAllThreeOff()
        {
            var user = TestHelper.SetPrincipal("TestUser01");
            ClearGateSettings();

            using (var repo = new Voat.Data.Repository(user))
            {
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED, "false", "TestUser01");
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED, "false", "TestUser01");
                await repo.SetSiteSetting(Voat.Data.Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED, "false", "TestUser01");
            }

            using (var repo = new Voat.Data.Repository(user))
            {
                var policy = repo.GetSubverseCreationPolicy();

                Assert.IsFalse(policy.AccountAgeEnabled, "The age gate must be off.");
                Assert.IsFalse(policy.CommentPointsEnabled, "The CCP gate must be off.");
                Assert.IsFalse(policy.SubmissionPointsEnabled, "The SCP gate must be off — this is the state that lets any account create a subverse.");
            }
        }

        /// <summary>
        /// Removes every gate row so each policy test starts from the "nothing
        /// stored" state the fallback is defined against. The keys are
        /// site-wide names, so without this the tests would depend on the order
        /// MSTest happens to run them in.
        /// </summary>
        private static void ClearGateSettings()
        {
            using (var db = new Voat.Data.Models.VoatDataContext())
            {
                var rows = db.SiteSetting
                    .Where(x => x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED
                             || x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS
                             || x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED
                             || x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS
                             || x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED
                             || x.Name == Voat.Data.Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS)
                    .ToList();

                if (rows.Count > 0)
                {
                    db.SiteSetting.RemoveRange(rows);
                    db.SaveChanges();
                }
            }
        }
    }
}
