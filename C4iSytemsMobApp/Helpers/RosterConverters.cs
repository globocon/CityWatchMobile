using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace C4iSytemsMobApp.Helpers
{
    /// <summary>
    /// [Roster Module] - Converts a boolean expanded state to an arrow icon.
    /// </summary>
    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // \uf078 is angle-down, \uf077 is angle-up in FontAwesome
            return (bool)value ? "\uf077" : "\uf078";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// [Roster Module] - Defines gradients for different shift types and statuses.
    /// </summary>
    public class ShiftStatusToGradientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.RosterShift shift)
            {
                // 1. Declined Status (Black) - Awaiting Relief
                // PRIORITY: This must come first so declined shifts are always black
                if (shift.StatusCode == 2)
                {
                    // Match the header dot color #212121 for consistency
                    return new SolidColorBrush(Color.FromArgb("#212121"));
                }

                // 2. Accepted Status (Green)
                if (shift.StatusCode == 1)
                {
                    bool isAdhoc = shift.ShiftType == "Adhoc";
                    return new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop { Color = Color.FromArgb(isAdhoc ? "#1B5E20" : "#90EE90"), Offset = 0.1f },
                            new GradientStop { Color = Color.FromArgb(isAdhoc ? "#2E7D32" : "#32CD32"), Offset = 1.0f }
                        }
                    };
                }

                // 3. Relief Shifts (Purple) - Pushed Status (Not Accepted) for Normal Shifts
                if (shift.ReliefGuardId != null && shift.ReliefGuardId > 0 && shift.ShiftType != "Adhoc")
                {
                    return new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop { Color = Color.FromArgb("#6f42c1"), Offset = 0.1f },
                            new GradientStop { Color = Color.FromArgb("#4a148c"), Offset = 1.0f }
                        }
                    };
                }

                // 4. Not Accepted Status (Orange)
                bool isAdhocNotAccepted = shift.ShiftType == "Adhoc";
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(isAdhocNotAccepted ? "#FF8F00" : "#FFB74D"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb(isAdhocNotAccepted ? "#E65100" : "#FB8C00"), Offset = 1.0f }
                    }
                };
            }

            return Brush.Orange;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// [Roster Module] - Highlights public holidays with a specific background color.
    /// </summary>
    public class HolidayToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isHoliday = (bool)value;
            if (isHoliday)
            {
                return Color.FromArgb("#FFF9C4"); // Light Yellow
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// [Roster Module] - Returns true if the value is not null. Used for visibility.
    /// </summary>
    public class NotNullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
                return !string.IsNullOrWhiteSpace(str);
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// [Roster Module] - Converts roster status (Paid, Invoiced, etc.) to a label color.
    /// </summary>
    public class RosterStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Colors.Gray;
            string status = value.ToString().ToLower();

            if (status.Contains("paid")) return Colors.Red; // Matching web stamp
            if (status.Contains("live")) return Colors.Green;
            if (status.Contains("inv")) return Colors.Blue; // Invoiced
            if (status.Contains("can")) return Colors.Gray; // Canceled
            
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// [Roster Module] - Returns true if an integer count is greater than zero.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
