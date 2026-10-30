using System;

namespace Voat.Domain.Models
{
    /// <summary>
    /// The handful of numbers the admin dashboard shows. Counts only — no
    /// trends, no per-day series. Deliberately not cached: five single-table
    /// COUNT(*)s are cheaper than the invalidation a cache would need, since
    /// the user count changes on every signup.
    /// </summary>
    public class SitewideStats
    {
        public int TotalUsers { get; set; }
        public int NewUsersLastSevenDays { get; set; }
        public int TotalSubmissions { get; set; }
        public int TotalComments { get; set; }
        public int TotalSubverses { get; set; }
    }
}
