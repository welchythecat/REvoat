using System;
using System.Collections.Generic;
using Voat.Domain.Models;

namespace Voat.UI.Areas.Admin.Models
{
    /// <summary>
    /// The admin user-lookup page. Users is null until a search has been run.
    /// </summary>
    public class UserLookupViewModel
    {
        public string Term { get; set; }

        public IEnumerable<UserSearchResult> Users { get; set; }
    }
}
