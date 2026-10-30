using System;
using Voat.Domain.Models;

namespace Voat.UI.Areas.Admin.Models
{
    /// <summary>
    /// One account on the admin user-detail page. Deliberately a flat view
    /// model rather than the full VoatIdentityUser — the admin panel shows a
    /// summary and must never surface password hashes or security stamps.
    /// </summary>
    public class AdminUserDetailViewModel
    {
        public string UserName { get; set; }

        public DateTime RegistrationDateTime { get; set; }

        /// <summary>
        /// Matches the entity: never null, but default(DateTime) when the
        /// account has never logged in.
        /// </summary>
        public DateTime LastLoginDateTime { get; set; }

        public string Bio { get; set; }

        public int SubmissionCount { get; set; }

        public int CommentCount { get; set; }

        /// <summary>Comment Contribution Points.</summary>
        public int CommentPoints { get; set; }

        /// <summary>Submission Contribution Points.</summary>
        public int SubmissionPoints { get; set; }

        /// <summary>Non-null when the account is globally banned.</summary>
        public Ban GlobalBan { get; set; }

        public bool IsGloballyBanned => GlobalBan != null;
    }
}
