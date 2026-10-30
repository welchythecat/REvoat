using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voat.Data;
using Voat.Data.Models;
using Voat.Domain.Models;
using Voat.Utilities;

namespace Voat.Data
{
    /// <summary>
    /// Reads behind the admin dashboard: the sitewide counts and the user
    /// lookup search. Deliberately uncached — five single-table COUNT(*)s and
    /// one paged lookup are cheaper than the invalidation a cache would need,
    /// since the user count changes on every signup.
    /// </summary>
    public partial class Repository
    {
        /// <summary>
        /// Returns the five dashboard counts. Two connections are involved:
        /// submissions, comments and subverses live in the content database,
        /// but AspNetUsers lives in the identity database, so the user counts
        /// have to be taken from CONNECTION_USERS rather than from _db.
        /// </summary>
        public async Task<SitewideStats> GetSitewideStats()
        {
            var stats = new SitewideStats();

            stats.TotalSubmissions = await ContentCount("Submission");
            stats.TotalComments = await ContentCount("Comment");
            stats.TotalSubverses = await ContentCount("Subverse");

            using (var db = new VoatDataContext(CONSTANTS.CONNECTION_USERS))
            {
                var usersTable = SqlFormatter.Table("AspNetUsers");

                stats.TotalUsers = await db.Connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {usersTable}");

                //AddDynamicParams, not Parameters.Add: the latter drops the
                //timezone when Npgsql sends a DateTime, which would make the
                //seven-day window wrong by whatever offset the server is on.
                //Same bug as Repository_User — see npgsql/npgsql#972.
                var q = new DapperQuery();
                q.Select = $"COUNT(*) FROM {usersTable}";
                q.Where = "\"RegistrationDateTime\" >= @StartDate";
                q.Parameters.AddDynamicParams(new { StartDate = CurrentDate.AddDays(-7) });

                stats.NewUsersLastSevenDays = await db.Connection.ExecuteScalarAsync<int>(q.ToString(), q.Parameters);
            }

            return stats;
        }

        /// <summary>
        /// Prefix search over user names for the admin lookup. Matches against
        /// NormalizedUserName (Identity upper-cases it) so the search is
        /// case-insensitive without a lower() on the column, which would stop
        /// the index being used.
        /// </summary>
        /// <param name="term">What the admin typed. Wildcards are escaped.</param>
        /// <param name="page">Zero-based page.</param>
        /// <param name="count">Page size, clamped to 25.</param>
        public async Task<IEnumerable<UserSearchResult>> SearchUsers(string term, int page = 0, int count = 25)
        {
            if (String.IsNullOrWhiteSpace(term))
            {
                return Enumerable.Empty<UserSearchResult>();
            }

            term = term.Trim();
            page = Math.Max(0, page);
            count = Math.Min(25, Math.Max(1, count));

            //Escape LIKE metacharacters so a search for "%" cannot turn into
            //a full table scan, and "_" does not swallow a real character.
            var prefix = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .ToUpperInvariant() + "%";

            using (var db = new VoatDataContext(CONSTANTS.CONNECTION_USERS))
            {
                var q = new DapperQuery();
                q.Select = $"\"UserName\", \"RegistrationDateTime\" FROM {SqlFormatter.Table("AspNetUsers")}";
                q.Where = "\"NormalizedUserName\" LIKE @Prefix";
                q.OrderBy = "\"NormalizedUserName\"";
                q.SkipCount = page * count;
                q.TakeCount = count;
                q.Parameters.Add("Prefix", prefix);

                return await db.Connection.QueryAsync<UserSearchResult>(q.ToString(), q.Parameters);
            }
        }

        /// <summary>Single-table count against the content database.</summary>
        private async Task<int> ContentCount(string tableName)
        {
            var q = new DapperQuery();
            q.Select = $"COUNT(*) FROM {SqlFormatter.Table(tableName)}";
            return await _db.Connection.ExecuteScalarAsync<int>(q.ToString(), q.Parameters);
        }
    }
}
