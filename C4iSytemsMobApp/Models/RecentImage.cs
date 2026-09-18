using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// A recent device-gallery image shown in the camera picker thumbnail strip.
    /// </summary>
    public class RecentImage : INotifyPropertyChanged
    {
        private bool _isSelected;
        private ImageSource? _thumbnail;

        public long MediaStoreId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Loaded lazily after the strip is shown, so the picker opens instantly.</summary>
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail == value) return;
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
