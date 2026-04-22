using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Helpers
{
    public class RosterStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RosterShift shift)
            {
                // Declined status always returns Black
                if (shift.StatusCode == (int)RosterShiftStatus.Declined)
                    return Colors.Black;

                // Accepted status logic
                if (shift.StatusCode == (int)RosterShiftStatus.Accepted)
                {
                    // Light Green for Regular, Dark Green for Adhoc
                    return (shift.ShiftType == "AdhocAccepted") ? Color.FromArgb("#006400") : Color.FromArgb("#90EE90");
                }

                // Pushed/Unassigned status logic (Default)
                // Orange for Regular, Dark Orange for Adhoc
                return (shift.ShiftType == "AdhocNotAccepted") ? Colors.DarkOrange : Colors.Orange;
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
