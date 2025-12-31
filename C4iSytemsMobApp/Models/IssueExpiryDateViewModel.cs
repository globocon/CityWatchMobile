using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;


namespace C4iSytemsMobApp.Models
{
    public class IssueExpiryDateViewModel : INotifyPropertyChanged
    {

        private bool _isExpiry = true;
        private DateTime? _expiryDate;
        private DateTime? _issueDate;
        private DateTime _displayDate = DateTime.Today;

        public IssueExpiryDateViewModel()
        {
            
        }
        public bool IsExpiry
        {
            get => _isExpiry;
            set
            {
                if (_isExpiry == value) return;

                _isExpiry = value;

                if (!_isExpiry)
                {
                    // Switching to Issue Date → clear expiry automatically
                    ExpiryDate = null;
                    DisplayDate = IssueDate ?? DateTime.Today;
                }
                else
                {
                    DisplayDate = ExpiryDate ?? DateTime.Today;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(DateLabelText));
                OnPropertyChanged(nameof(MaximumDate));
                OnPropertyChanged(nameof(IsIssueToggle));
            }
        }

        public bool IsIssueToggle
        {
            get => !IsExpiry;
            set
            {
                IsExpiry = !value;
                OnPropertyChanged();
            }
        }

        public DateTime DisplayDate
        {
            get => _displayDate;
            set
            {
                _displayDate = value;

                if (IsExpiry)
                    ExpiryDate = value;
                else
                    IssueDate = value;

                OnPropertyChanged();
            }
        }

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set
            {
                _expiryDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime? IssueDate
        {
            get => _issueDate;
            set
            {
                if (value.HasValue && value.Value.Date > DateTime.Today)
                    return; // validation: issue date cannot be future

                _issueDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime MaximumDate =>
            IsExpiry ? DateTime.MaxValue : DateTime.Today;

        public string DateLabelText =>
            IsExpiry ? "Expiry Date (DOE)" : "Issue Date (DOI)";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
