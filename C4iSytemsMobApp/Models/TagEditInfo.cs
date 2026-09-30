namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// A tag looked up for editing - Scanner/GetTagForEdit.
    ///
    /// Distinct from <see cref="TagInfoApiResponse"/>, which is the patrol scan response and
    /// carries hit-log fields. This is a plain read of the tag record: who it belongs to, and
    /// what its description currently says.
    /// </summary>
    public class TagEditInfo
    {
        public bool IsSuccess { get; set; }

        /// <summary>False when the UID is not registered - the "Tag not found in database" case.</summary>
        public bool tagFound { get; set; }

        public string message { get; set; }

        /// <summary>ClientSiteSmartWandTags.Id - needed so the save updates instead of inserting.</summary>
        public int Id { get; set; }

        /// <summary>The site the tag belongs to, which is not necessarily the site the guard is at.</summary>
        public int ClientSiteId { get; set; }

        public string ClientSiteName { get; set; }

        public string UId { get; set; }

        public int TagsTypeId { get; set; }

        public string TagsType { get; set; }

        /// <summary>The description as it stands - the "before" half of the logbook entry.</summary>
        public string LabelDescription { get; set; }
    }
}
