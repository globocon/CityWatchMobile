using System.ComponentModel;

namespace C4iSytemsMobApp.Models
{
    /// <summary>CityWatch.Data.Enums.GuardNotificationTarget — who the notification is for.</summary>
    public enum GuardNotificationTarget
    {
        Guard = 1,
        Site = 2
    }

    /// <summary>
    /// One card in the Notifications tab. Mirrors GuardNotificationDTO on the server.
    ///
    /// A notification is addressed either to this guard or to the site they are signed in at;
    /// the card header says which, and <see cref="TargetName"/> names them. Read state is per
    /// guard even for site notifications — one guard reading a site message must not clear it
    /// for the rest of the shift — so the server resolves IsRead for the guard asking.
    ///
    /// Implements INotifyPropertyChanged because IsRead is toggled in place from the card: the
    /// list is not reloaded after a mark read/unread, so the card has to repaint itself.
    /// </summary>
    public class GuardNotification : INotifyPropertyChanged
    {
        private bool _isRead;

        public int Id { get; set; }

        /// <summary>Set for guard-targeted notifications; null for site-targeted ones.</summary>
        public int? GuardId { get; set; }

        /// <summary>Set for site-targeted notifications; null for guard-targeted ones.</summary>
        public int? ClientSiteId { get; set; }

        public int NotificationTypeId { get; set; }
        public int ReferenceId { get; set; }

        /// <summary>1 = Guard, 2 = Site. Set by the server from whichever id is populated.</summary>
        public int Target { get; set; }

        /// <summary>Guard name or site name, as supplied by the server.</summary>
        public string TargetName { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ReadOn { get; set; }

        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead == value)
                    return;

                _isRead = value;
                OnPropertyChanged(nameof(IsRead));
                OnPropertyChanged(nameof(IsUnread));
                OnPropertyChanged(nameof(ReadActionText));
            }
        }

        /// <summary>Drives the unread dot and the bolder card styling.</summary>
        public bool IsUnread => !_isRead;

        /// <summary>The label on the card's toggle — it always offers the opposite state.</summary>
        public string ReadActionText => _isRead ? "Mark as unread" : "Mark as read";

        public string CreatedOnText => CreatedOn.ToString("dd MMM yyyy @ HH:mm");

        public bool IsGuardNotification => Target == (int)GuardNotificationTarget.Guard;

        /// <summary>
        /// The explicit inverse exists so the card header can pick its chip with plain
        /// IsVisible bindings instead of a DataTrigger comparing against False — boolean
        /// visibility is unambiguous, trigger value conversion is not.
        /// </summary>
        public bool IsSiteNotification => !IsGuardNotification;

        /// <summary>
        /// Who the card is addressed to, beside the chip. Falls back to a generic word when the
        /// server could not resolve a name (a deleted guard or site) so the header never reads
        /// as an empty gap.
        /// </summary>
        public string TargetDisplayName =>
            string.IsNullOrWhiteSpace(TargetName)
                ? (IsGuardNotification ? "You" : "Your site")
                : TargetName;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
