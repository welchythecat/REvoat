namespace Voat.UI.Areas.Admin.Models
{
    /// <summary>
    /// The admin "Site settings" page. The values shown are the effective ones:
    /// the saved database row when one exists, otherwise the appsettings.json
    /// value the site would enforce today. Saving writes all six rows.
    /// </summary>
    public class SiteSettingsViewModel
    {
        public bool SubverseCreationAgeGateEnabled { get; set; }
        public int SubverseCreationMinAccountAgeDays { get; set; }

        public bool SubverseCreationCcpGateEnabled { get; set; }
        public int SubverseCreationMinCommentPoints { get; set; }

        public bool SubverseCreationScpGateEnabled { get; set; }
        public int SubverseCreationMinSubmissionPoints { get; set; }
    }
}
