namespace Voat.Domain.Models
{
    /// <summary>
    /// The effective subverse-creation gates, resolved from the SiteSetting rows
    /// when present and from VoatSettings when they are not. Holding the
    /// resolved values means the rule never has to know which source won.
    ///
    /// All three gates share the same shape — an on/off switch and a threshold —
    /// so they are stored and read the same way. A gate that is switched off
    /// ignores its threshold entirely.
    /// </summary>
    public class SubverseCreationPolicy
    {
        public bool AccountAgeEnabled { get; set; }

        /// <summary>Minimum account age in days. Ignored when AccountAgeEnabled is false.</summary>
        public int MinimumAccountAgeDays { get; set; }

        public bool CommentPointsEnabled { get; set; }

        /// <summary>Minimum comment (CCP) points. Ignored when CommentPointsEnabled is false.</summary>
        public int MinimumCommentPoints { get; set; }

        public bool SubmissionPointsEnabled { get; set; }

        /// <summary>Minimum submission (SCP) points. Ignored when SubmissionPointsEnabled is false.</summary>
        public int MinimumSubmissionPoints { get; set; }
    }
}
