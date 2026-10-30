using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Voat.Common;
using Voat.Data;
using Voat.Domain.Models;

namespace Voat.UI.Areas.Admin.Controllers
{
    /// <summary>
    /// Sitewide reports queue. A null subverse to GetRuleReports means
    /// "every subverse", which is exactly what the admin panel needs, so no
    /// new report SQL is written.
    /// </summary>
    public class ReportsController : BaseAdminController
    {
        // GET /admin/Reports?type=&days=&status=&ruleid=
        [HttpGet]
        public async Task<ActionResult> Index(ContentType? type = null, int days = 1, ReviewStatus status = ReviewStatus.Unreviewed, int[] ruleid = null)
        {
            days = days.EnsureRange(1, 7);

            using (var repo = new Repository(User))
            {
                var data = await repo.GetRuleReports(null, type, days * 24, status, ruleid);
                ViewBag.Days = days;
                ViewBag.RuleID = ruleid;
                ViewBag.ReviewStatus = status;
                ViewBag.Type = type;
                return View(data);
            }
        }
    }
}
