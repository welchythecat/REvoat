using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Command;
using Voat.Domain.Models;
using Voat.Domain.Query;
using Voat.Models.ViewModels;
using Voat.UI.Areas.Admin.Models;
using Voat.Utilities;

namespace Voat.UI.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin user lookup: search by name prefix, then look at one account and
    /// ban or unban it from there.
    /// </summary>
    public class UsersController : BaseAdminController
    {
        // GET /admin/Users?q=term
        public async Task<ActionResult> Index(string q)
        {
            var model = new UserLookupViewModel { Term = q };

            if (!String.IsNullOrWhiteSpace(q))
            {
                using (var repo = new Repository(User))
                {
                    model.Users = await repo.SearchUsers(q);
                }
            }

            return View(model);
        }

        // GET /admin/Users/Detail?name=x
        public async Task<ActionResult> Detail(string name)
        {
            var model = await BuildDetail(name);
            if (model == null)
            {
                return ErrorView(ErrorViewModel.GetErrorViewModel(ErrorType.NotFound));
            }
            return View(model);
        }

        // POST /admin/Users/Ban — ban from the detail page
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> Ban(string name, string reason)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return RedirectToAction(nameof(Index));
            }

            if (String.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError(String.Empty, "A reason is required.");
            }

            if (!ModelState.IsValid)
            {
                //Re-render the detail page with the error rather than losing
                //what the admin typed.
                var model = await BuildDetail(name);
                return model == null
                    ? ErrorView(ErrorViewModel.GetErrorViewModel(ErrorType.NotFound))
                    : (ActionResult)View(nameof(Detail), model);
            }

            var cmd = new GlobalBanCommand(
                new[] { new GenericReference<BanType>() { Name = name, Type = BanType.User } },
                reason).SetUserContext(User);

            var response = await cmd.Execute();
            if (!response.Success)
            {
                ModelState.AddModelError(String.Empty, response.Message);
                var model = await BuildDetail(name);
                return model == null
                    ? ErrorView(ErrorViewModel.GetErrorViewModel(ErrorType.NotFound))
                    : (ActionResult)View(nameof(Detail), model);
            }

            //Post/Redirect/Get — reloading the detail page now shows the ban.
            return RedirectToAction(nameof(Detail), new { name });
        }

        /// <summary>
        /// Assembles the detail view model, or null when the account does not
        /// exist. Name resolution goes through UserHelper so a differently
        /// cased spelling still finds the account.
        /// </summary>
        private async Task<AdminUserDetailViewModel> BuildDetail(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var canonical = UserHelper.OriginalUsername(name.Trim());
            if (String.IsNullOrEmpty(canonical))
            {
                return null;
            }

            var model = new AdminUserDetailViewModel { UserName = canonical };

            //RegistrationDateTime / LastLoginDateTime live on the identity
            //record; QueryUserRecord reads through VoatUserManager.
            var identity = await new QueryUserRecord(canonical).ExecuteAsync();
            if (identity != null)
            {
                model.RegistrationDateTime = identity.RegistrationDateTime;
                model.LastLoginDateTime = identity.LastLoginDateTime;
            }

            using (var repo = new Repository(User))
            {
                model.SubmissionCount = repo.UserContributionCount(canonical, ContentType.Submission);
                model.CommentCount = repo.UserContributionCount(canonical, ContentType.Comment);
                model.GlobalBan = repo.UserBan(canonical);
            }

            var info = await new QueryUserInformation(canonical).ExecuteAsync();
            if (info != null)
            {
                model.Bio = info.Bio;
                model.CommentPoints = info.CommentPoints?.Sum ?? 0;
                model.SubmissionPoints = info.SubmissionPoints?.Sum ?? 0;
            }

            return model;
        }
    }
}
