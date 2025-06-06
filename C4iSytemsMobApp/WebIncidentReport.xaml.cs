namespace C4iSytemsMobApp;

public partial class WebIncidentReport : ContentPage
{
    public WebIncidentReport()
    {
        InitializeComponent();
        IncidentDatePicker.Date = DateTime.Today;        
        JobTimePicker.Time = DateTime.Now.TimeOfDay; // Set default time

    }
    private async void OnHomeClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage();
    }

}