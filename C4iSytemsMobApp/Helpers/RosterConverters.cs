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
                // 1. Relief Shifts (Purple)
                if (shift.ReliefGuardId != null)
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

                // 2. Adhoc Shifts
                if (shift.ShiftType == "Adhoc")
                {
                    // Accepted (Green)
                    if (shift.StatusCode == 1)
                    {
                        return new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops = new GradientStopCollection
                            {
                                new GradientStop { Color = Color.FromArgb("#1B5E20"), Offset = 0.1f },
                                new GradientStop { Color = Color.FromArgb("#2E7D32"), Offset = 1.0f }
                            }
                        };
                    }
                    // Not Accepted / Pushed (Dark Orange)
                    else
                    {
                        return new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1),
                            GradientStops = new GradientStopCollection
                            {
                                new GradientStop { Color = Color.FromArgb("#FF8F00"), Offset = 0.1f },
                                new GradientStop { Color = Color.FromArgb("#E65100"), Offset = 1.0f }
                            }
                        };
                    }
                }

                // 3. Regular Shifts (Orange)
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#FFB74D"), Offset = 0.1f },
                        new GradientStop { Color = Color.FromArgb("#FB8C00"), Offset = 1.0f }
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
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
