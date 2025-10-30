
using C4iSytemsMobApp.Models;
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class EditPatrolCarLogPopup : Popup
{
    //private readonly PatrolCarLog _item;
    //private readonly Dictionary<string, Entry> _editEntries = new();

    public PatrolCarLog Log { get; set; }

    public EditPatrolCarLogPopup(PatrolCarLog log)
    {
        InitializeComponent();

        // Clone to avoid modifying original directly until Save
        Log = new PatrolCarLog
        {
            Id = log.Id,
            PatrolCarId = log.PatrolCarId,
            ClientSiteLogBookId = log.ClientSiteLogBookId,
            Mileage = log.Mileage,
            MileageText = log.MileageText,
            ClientSitePatrolCar = log.ClientSitePatrolCar,
            PatrolCar = log.PatrolCar
        };

        BindingContext = this;
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null); // return null to indicate cancelled
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {

        if (!decimal.TryParse(MileageEntry.Text, out var mileage))
        {
            // optional: show validation message
            Application.Current.MainPage.DisplayAlert("Validation", "Please enter a valid number.", "OK");
            return;
        }

        mileage = Math.Round(mileage, 0);
        Log.Mileage = mileage;
        Log.MileageText = mileage.ToString("N0");  // keep text in sync
        Close(Log);        
    }
}
