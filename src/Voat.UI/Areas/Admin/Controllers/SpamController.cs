using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Data.Models;
using Voat.Domain.Command;
using Voat.Domain.Models;
using Voat.Domain.Query;
using Voat.UI.Areas.Admin.Models;
using Voat.Utilities;

namespace Voat.UI.Areas.Admin.Controllers
{

    public class SpamController : BaseAdminController
    {

        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public ActionResult Ban()
        {
            return View(new BanViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> Ban(BanViewModel model)
        {
            if (ModelState.IsValid)
            {
                var banList = model.Name.Split(',', ';').Select(x =>
                    new Domain.Models.GenericReference<Domain.Models.BanType>() {
                        Name = x.TrimSafe(),
                        Type = model.BanType
                    }).ToList();


                var cmd = new GlobalBanCommand(banList, model.Reason).SetUserContext(User);
                var response = await cmd.Execute();
                if (response.Success)
                {
                    ViewBag.SuccessMessage = "Bans Saved";
                }
                else
                {
                    ModelState.AddModelError("", response.Message);
                    return View("Ban", model);
                }
            }
            return View("Ban", new BanViewModel());
        }

        // ---- listing existing bans --------------------------------------
        // Read-only, so these inherit the class-level five-role gate: a
        // GlobalJanitor can see the lists but the views hide the remove links.

        public async Task<ActionResult> BannedUsers()
        {
            var options = new SearchOptions(this.Request.QueryString.Value);
            var results = await new QueryBannedUsers().ExecuteAsync();

            if (!String.IsNullOrEmpty(options.Phrase))
            {
                var phrase = options.Phrase.ToLower();
                results = results.Where(x =>
                    (x.UserName ?? String.Empty).ToLower().Contains(phrase)
                    || (x.Reason ?? String.Empty).ToLower().Contains(phrase));
            }

            var page = results.Skip(options.Index).Take(options.Count);
            return View(new PaginatedList<BannedUser>(page, options.Page, options.Count));
        }

        public async Task<ActionResult> BannedDomains()
        {
            var options = new SearchOptions(this.Request.QueryString.Value);
            var results = await new QueryBannedDomains().ExecuteAsync();

            if (!String.IsNullOrEmpty(options.Phrase))
            {
                var phrase = options.Phrase.ToLower();
                results = results.Where(x =>
                    (x.Domain ?? String.Empty).ToLower().Contains(phrase)
                    || (x.Reason ?? String.Empty).ToLower().Contains(phrase));
            }

            var page = results.Skip(options.Index).Take(options.Count);
            return View(new PaginatedList<BannedDomain>(page, options.Page, options.Count));
        }

        // ---- removing a ban ---------------------------------------------
        // GET confirms, POST executes, then redirects back to the list
        // (Post/Redirect/Get) — the same shape as
        // SubverseModerationController.RemoveBan.

        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> RemoveUserBan(string name)
        {
            var ban = await FindUserBan(name);
            if (ban == null)
            {
                return RedirectToAction(nameof(BannedUsers));
            }
            return View(ban);
        }

        [HttpPost, ActionName(nameof(RemoveUserBan))]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> RemoveUserBanPost(string name)
        {
            var ban = await FindUserBan(name);
            if (ban == null)
            {
                return RedirectToAction(nameof(BannedUsers));
            }

            var cmd = new GlobalUnbanCommand(new[] {
                new GenericReference<BanType>() { Name = ban.UserName, Type = BanType.User }
            }).SetUserContext(User);

            var response = await cmd.Execute();
            if (!response.Success)
            {
                ModelState.AddModelError(String.Empty, response.Message);
                return View(nameof(RemoveUserBan), ban);
            }
            return RedirectToAction(nameof(BannedUsers));
        }

        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> RemoveDomainBan(string name)
        {
            var ban = await FindDomainBan(name);
            if (ban == null)
            {
                return RedirectToAction(nameof(BannedDomains));
            }
            return View(ban);
        }

        [HttpPost, ActionName(nameof(RemoveDomainBan))]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GlobalAdmin,Admin,DelegateAdmin,GlobalBans")]
        public async Task<ActionResult> RemoveDomainBanPost(string name)
        {
            var ban = await FindDomainBan(name);
            if (ban == null)
            {
                return RedirectToAction(nameof(BannedDomains));
            }

            var cmd = new GlobalUnbanCommand(new[] {
                new GenericReference<BanType>() { Name = ban.Domain, Type = BanType.Domain }
            }).SetUserContext(User);

            var response = await cmd.Execute();
            if (!response.Success)
            {
                ModelState.AddModelError(String.Empty, response.Message);
                return View(nameof(RemoveDomainBan), ban);
            }
            return RedirectToAction(nameof(BannedDomains));
        }

        /// <summary>
        /// Looks a ban up in the cached query rather than opening a fresh
        /// connection — the same source the list page renders from, so the
        /// confirm page can never disagree with the list about what exists.
        /// </summary>
        private static async Task<BannedUser> FindUserBan(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            var results = await new QueryBannedUsers().ExecuteAsync();
            return results.FirstOrDefault(x =>
                String.Equals(x.UserName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<BannedDomain> FindDomainBan(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            var results = await new QueryBannedDomains().ExecuteAsync();
            return results.FirstOrDefault(x =>
                String.Equals(x.Domain, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
