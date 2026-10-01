namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// The guard's identity as the office holds it - Scanner/GetGuardNameDetails, read from the
    /// Guards table.
    ///
    /// Used when the app composes a logbook entry that names the person who made a change.
    /// Deliberately not taken from Preferences: those were written at some earlier login and a
    /// renamed or re-badged guard would be recorded under the old details forever.
    /// </summary>
    public class GuardNameDetails
    {
        public bool IsSuccess { get; set; }

        public string message { get; set; }

        public int Id { get; set; }

        public string Name { get; set; }

        public string SecurityNo { get; set; }

        public string Initial { get; set; }
    }
}
