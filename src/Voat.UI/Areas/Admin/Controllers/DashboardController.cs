using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Voat.Data;
using Voat.Domain.Models;

namespace Voat.UI.Areas.Admin.Controllers
{
    /// <summary>
    /// The admin panel's home page. Reached at /admin — see
    /// Areas/Admin/RouteConfig.cs, where the area's default controller/action
    /// was moved here from Spam/Ban.
    /// </summary>
    public class DashboardController : BaseAdminController
    {
        public async Task<ActionResult> Index()
        {
            using (var repo = new Repository(User))
            {
                var stats = await repo.GetSitewideStats();
                return View(stats);
            }
        }
    }
}
