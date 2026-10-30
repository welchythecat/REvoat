using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Voat.Configuration;
using Voat.Data.Models;
using Voat.Domain.Models;
using Voat.Utilities;

namespace Voat.Data
{
    /// <summary>
    /// Storage for owner-editable site settings. Each setting is one row, named
    /// by a dotted key, so new settings can be added without a schema change.
    ///
    /// A missing row means "use the appsettings.json value" rather than a
    /// default of its own: seeding these rows would permanently shadow config
    /// for operators who change VoatSettings later.
    /// </summary>
    public partial class Repository
    {
        public const string SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED = "subverseCreation.minAccountAge.enabled";
        public const string SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS = "subverseCreation.minAccountAge.days";
        public const string SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED = "subverseCreation.minCommentPoints.enabled";
        public const string SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS = "subverseCreation.minCommentPoints.points";
        public const string SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED = "subverseCreation.minSubmissionPoints.enabled";
        public const string SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS = "subverseCreation.minSubmissionPoints.points";

        /// <summary>
        /// Returns the stored value, or null when the row does not exist.
        /// Synchronous because the rules engine calls this from
        /// Rule.EvaluateRule, which cannot await.
        /// </summary>
        public string GetSiteSetting(string name)
        {
            return _db.SiteSetting
                .Where(x => x.Name == name)
                .Select(x => x.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Inserts or updates a setting. Updates in place so the row keeps a
        /// single author/date stamp rather than accumulating history.
        /// </summary>
        public async Task SetSiteSetting(string name, string value, string userName)
        {
            var setting = await _db.SiteSetting
                .Where(x => x.Name == name)
                .FirstOrDefaultAsync()
                .ConfigureAwait(CONSTANTS.AWAIT_CAPTURE_CONTEXT);

            if (setting == null)
            {
                setting = new SiteSetting { Name = name };
                _db.SiteSetting.Add(setting);
            }

            setting.Value = value;
            setting.UpdatedBy = userName;
            setting.LastUpdateDate = Repository.CurrentDate;

            await _db.SaveChangesAsync().ConfigureAwait(CONSTANTS.AWAIT_CAPTURE_CONTEXT);
        }

        /// <summary>
        /// Resolves all three subverse-creation gates at once. The stored rows
        /// win when present, otherwise VoatSettings supplies the same defaults
        /// the site has always enforced. A value that will not parse is treated
        /// as absent rather than being allowed to disable a gate by accident.
        ///
        /// Synchronous — see GetSiteSetting.
        /// </summary>
        public SubverseCreationPolicy GetSubverseCreationPolicy()
        {
            var rows = _db.SiteSetting
                .Where(x => x.Name == SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED
                         || x.Name == SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS
                         || x.Name == SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED
                         || x.Name == SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS
                         || x.Name == SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED
                         || x.Name == SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS)
                .Select(x => new { x.Name, x.Value })
                .ToList()
                .ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

            return new SubverseCreationPolicy
            {
                AccountAgeEnabled = ReadEnabled(rows, SETTING_SUBVERSE_CREATION_AGE_GATE_ENABLED, true),
                MinimumAccountAgeDays = ReadInt(rows, SETTING_SUBVERSE_CREATION_AGE_GATE_DAYS,
                    VoatSettings.Instance.MinimumAccountAgeInDaysForSubverseCreation, MAX_ACCOUNT_AGE_DAYS),

                CommentPointsEnabled = ReadEnabled(rows, SETTING_SUBVERSE_CREATION_CCP_GATE_ENABLED, true),
                MinimumCommentPoints = ReadInt(rows, SETTING_SUBVERSE_CREATION_CCP_GATE_POINTS,
                    VoatSettings.Instance.MinimumCommentPointsForSubverseCreation, MAX_GATE_POINTS),

                SubmissionPointsEnabled = ReadEnabled(rows, SETTING_SUBVERSE_CREATION_SCP_GATE_ENABLED, true),
                MinimumSubmissionPoints = ReadInt(rows, SETTING_SUBVERSE_CREATION_SCP_GATE_POINTS,
                    VoatSettings.Instance.MinimumSubmissionPointsForSubverseCreation, MAX_GATE_POINTS)
            };
        }

        //Bounds applied to whatever is stored, so a hand-edited row cannot
        //produce a negative threshold or an unreachable one. The admin page
        //clamps to the same ranges on the way in.
        public const int MAX_ACCOUNT_AGE_DAYS = 3650;
        public const int MAX_GATE_POINTS = 100000;

        /// <summary>
        /// A gate is on unless a row explicitly stores "false". An unparseable
        /// row falls back to the default rather than silently turning the gate
        /// off.
        /// </summary>
        private static bool ReadEnabled(Dictionary<string, string> rows, string name, bool defaultValue)
        {
            if (!rows.TryGetValue(name, out string stored))
            {
                return defaultValue;
            }
            return Boolean.TryParse(stored, out bool parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// A threshold comes from its row when the row parses, and from
        /// appsettings.json otherwise. Clamped to the given range.
        /// </summary>
        private static int ReadInt(Dictionary<string, string> rows, string name, int defaultValue, int maximum)
        {
            if (!rows.TryGetValue(name, out string stored) || !Int32.TryParse(stored, out int parsed))
            {
                parsed = defaultValue;
            }
            return Math.Max(0, Math.Min(maximum, parsed));
        }
    }
}
