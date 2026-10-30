namespace Voat.Data.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single owner-editable site setting, stored as a name/value pair so more
    /// settings can be added later without a schema change. Name is a dotted key
    /// such as "subverseCreation.minAccountAge.days".
    /// </summary>
    public partial class SiteSetting
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string UpdatedBy { get; set; }
        public Nullable<System.DateTime> LastUpdateDate { get; set; }
    }
}
