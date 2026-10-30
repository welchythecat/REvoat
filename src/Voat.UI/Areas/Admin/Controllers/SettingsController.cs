using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Voat.Data;
using Voat.UI.Areas.Admin.Models;

namespace Voat.UI.Areas.Admin.Controllers
{
    /// <summary>
    /// Owner-editable site settings, at /admin/Settings.
    ///
    /// Narrower than the rest of the admin panel: only GlobalAdmin and Admin
    /// may read or change these, on top of the five-role gate BaseAdminController
    /// already applies. ASP.NET Core combines multiple [Authorize] attributes
    /// with AND, so the effective set is exactly those two roles.
    /// </summary>
    public class SettingsController : BaseAdminController
    {
        // GET /admin/Settings
        [Authorize(Roles = "GlobalAdmin,Admin")]
        public ActionResult Index()
        {
            using (var repo = new Repository(User))
            {
                // Read through the same resolver the rule uses, so the page
                // always shows what is actually being enforced right now.
                var policy = repo.GetSubverseCreationPolicy();
                return View(new SiteSettingsViewModel
                {
                    SubverseCreationAgeGateEnabled = policy.AccountAgeEnabled,
                    SubverseCreationMinAccountAgeDays = policy.MinimumAccountAgeDays,
                    SubverseCreationCcpGateEnabled = policy.CommentPointsEnabled,
                    SubverseCreationMinCommentPoints = policy.MinimumCommentPoints,
                    SubverseCreationScpGateEnabled = policy.SubmissionPointsEnabled,
                    SubverseCreationMinSubmissionPoints = policy.MinimumSubmissionPoints
                });
            }
        }

        // POST /admin/Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GlobalAdmin,Admin")]
        public async Task<ActionResult> Index(SiteSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // The number fields are free text, so a non-numeric entry lands
                // here as a binding failure with no message of its own.
                ModelState.AddModelError(String.Empty, "Each threshold must be a whole number.");
                return View(model);
            }

            model.SubverseCreationMinAccountAgeDays =
                Clamp(model.SubverseCreationMinAccountAgeDays, Repository.MAX_ACCOUNT_AGE_DAYS);
            model.SubverseCreationMinCommentPoints =
                Clamp(model.SubverseCreationMinCommentPoints, Repository.MAX_GATE_POINTS);
            model.SubverseCreationMinSubmissionPoints =
                Clamp(model.SubverseCreationMinSubmissionPoints, Repository.MAX_GATE_POINTS);

            using (var repo = new Repository(User))
            {
                var by = User.Identity.Name;

                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED,
                    OnOff(model.SubverseCreationAgeGateEnabled), by);
                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS,
                    model.SubverseCreationMinAccountAgeDays.ToString(), by);

                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED,
                    OnOff(model.SubverseCreationCcpGateEnabled), by);
                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS,
                    model.SubverseCreationMinCommentPoints.ToString(), by);

                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED,
                    OnOff(model.SubverseCreationScpGateEnabled), by);
                await repo.SetSiteSetting(Repository.SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS,
                    model.SubverseCreationMinSubmissionPoints.ToString(), by);
            }

            // Post/Redirect/Get so a refresh can't re-submit the form.
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Stores booleans the way the settings reader parses them back.
        /// </summary>
        private static string OnOff(bool enabled)
        {
            return enabled ? "true" : "false";
        }

        /// <summary>
        /// Bounds a threshold so a typo can't become negative or unreachable.
        /// Matches the clamp the settings reader applies on the way out.
        /// </summary>
        private static int Clamp(int value, int maximum)
        {
            return Math.Max(0, Math.Min(maximum, value));
        }
    }
}
