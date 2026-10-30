using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using System;
using Voat.Configuration;
using Voat.Domain.Models;
using Voat.Utilities;
using Microsoft.AspNetCore.Routing.Constraints;
using Voat.Models.ViewModels;
using Voat.Common;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Voat.UI.Areas.Admin
{
    public class RouteConfig
    {
        public static void RegisterRoutes(IRouteBuilder routes)
        {
            //The area's default fills in the missing trailing segments of the
            //{area}/{controller}/{action} template, so this is what bare /admin
            //resolves to. It used to point at Spam/Ban; it is now the
            //dashboard, which links onward to the ban utility at
            ///admin/Spam/Ban (that URL is unchanged).
            RerouteDefaultArea(routes, "Admin", VoatSettings.Instance.AreaMaps, new { controller = "Dashboard", action = "Index" });
        }
        private static void RerouteDefaultArea(IRouteBuilder routes, string areaName, IDictionary<string, string> areaMaps, object defaults = null)
        {
            var altName = areaName;

            //If config has an areaMap for admin re-route here
            if (areaMaps != null && areaMaps.Any() && areaMaps.Any(x => x.Key.IsEqual(areaName)))
            {
                var keyPair = VoatSettings.Instance.AreaMaps.FirstOrDefault(x => x.Key.IsEqual(areaName));
                altName = keyPair.Value;
            }

            routes.MapRoute(
                name: areaName,
                template: $"{{area:regex({altName})}}/{{controller}}/{{action}}", //Attn: Future People: Need to pull out these defaults. Hello past person, ok.
                defaults: defaults == null ? new { } : defaults,
                constraints: new { area = altName }
            );
        }
    }
}
