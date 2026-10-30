using System;

namespace Voat.Domain.Models
{
    /// <summary>
    /// One row of the admin user-lookup search. Kept to name + registration
    /// date because that is all the lookup list needs — everything else is
    /// loaded by the per-user detail page.
    /// </summary>
    public class UserSearchResult
    {
        public string UserName { get; set; }
        public DateTime RegistrationDateTime { get; set; }
    }
}
