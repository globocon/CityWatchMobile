using C4iSytemsMobApp.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace C4iSytemsMobApp;

public partial class WebIncidentReport : ContentPage, INotifyPropertyChanged
{

    
    public ObservableCollection<string> UploadedFiles { get; set; } = new();
    private List<string> uploadedServerFileNames = new(); // filenames returned by server
    private readonly string[] allowedExtensions = new[] {
        ".avi", ".bmp", ".jpeg", ".jpg", ".mp3", ".mp4", ".pdf", ".png", ".xlsx", ".heic", ".gif"
    };
    private string _selectedFeedbackType;
    private FeedbackTemplateViewModel _selectedTemplate;
    private string _savedClientAddress;

    private string _backupAddress = string.Empty;
    private List<FeedbackTemplateViewModel> _feedbackTemplates = new();
    public ObservableCollection<FeedbackTemplateViewModel> ColourCodeList { get; set; } = new();
    public ObservableCollection<string> TemplateTypesList { get; set; } = new();
    public ObservableCollection<FeedbackTemplateViewModel> FilteredTemplatesList { get; set; } = new();

    private ClientSite _currentClientSite;


    private string _clientAddress;
    private bool _isSuggestionsVisible;

    private ObservableCollection<string> _suggestions = new();
    public ObservableCollection<string> Suggestions
    {
        get => _suggestions;
        set
        {
            _suggestions = value;
            OnPropertyChanged(nameof(Suggestions));
            //IsSuggestionsVisible = _suggestions.Any();
        }
    }

    private bool _isSearchEnabled;
    public bool IsSearchEnabled
    {
        get => _isSearchEnabled;
        set
        {
            if (_isSearchEnabled != value)
            {
                _isSearchEnabled = value;
                OnPropertyChanged(nameof(IsSearchEnabled));
            }
        }
    }

    public string ClientAddress
    {
        get => _clientAddress;
        set
        {
            if (_clientAddress != value)
            {
                _clientAddress = value;
                OnPropertyChanged(nameof(ClientAddress));

                // Only load suggestions if search is active AND not empty
                if (IsSearchEnabled && !string.IsNullOrWhiteSpace(_clientAddress))
                {
                    _ = LoadSuggestionsAsync(_clientAddress);
                    IsSuggestionsVisible = true; // 
                }
                else
                {
                    Suggestions.Clear();
                    IsSuggestionsVisible = false;
                }
            }
        }
    }

    public bool IsSuggestionsVisible
    {
        get => _isSuggestionsVisible;
        set
        {
            _isSuggestionsVisible = value;
            OnPropertyChanged(nameof(IsSuggestionsVisible));
        }
    }

    private readonly GooglePlacesService _placesService = new();

    private async Task LoadSuggestionsAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Suggestions.Clear();
            return;
        }

        var results = await _placesService.GetSuggestionsAsync(input);
        Suggestions = new ObservableCollection<string>(results);
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    public async Task<(double Lat, double Lng)?> GetCoordinatesFromPlaceIdAsync(string placeId)
    {
        try
        {
            var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key=AIzaSyCJK5DRhsD9rePFC-p9_8schzBdZsmXfUs";
            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);

            using var json = JsonDocument.Parse(response);
            var result = json.RootElement.GetProperty("result");
            var location = result.GetProperty("geometry").GetProperty("location");

            double lat = location.GetProperty("lat").GetDouble();
            double lng = location.GetProperty("lng").GetDouble();

            return (lat, lng);
        }
        catch
        {
            return null;
        }
    }
    public static class IrSession
    {
        public static string ReportReference { get; set; } = Guid.NewGuid().ToString();
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await LoadFeedbackTemplates();

            var clientTypeRaw = await SecureStorage.GetAsync("ClientSite");
            var clientSite = await GetClientSiteByName(clientTypeRaw);
            await LoadClientAreas(clientSite.Id);
            if (clientSite != null)
            {
                _currentClientSite = clientSite;
                clientAddressEntry.Text = clientSite.Address; //
            }
            else
            {
                await DisplayAlert("Not Found", $"No site found with the name '{clientTypeRaw}'.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load client site info.\n\n{ex.Message}", "OK");
        }
    }



    public WebIncidentReport()
    {
        InitializeComponent();
        BindingContext = this; // or to your ViewModel
        //var viewModel = new LocationViewModel();
        //BindingContext = viewModel;

        reportDatePickerOffsite.Date = DateTime.Today;
        reportTimePickerOffsite.Time = DateTime.Now.TimeOfDay;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => ToggleDropdown();
        DropdownBorder.GestureRecognizers.Add(tapGesture);
        DropdownBorder.IsEnabled = false;

        // Now you can safely call:
        //viewModel.Suggestions.Clear();
        //viewModel.IsSuggestionsVisible = false;
    }

    public async Task<ClientSite> GetClientSiteByName(string name)
    {
        using var httpClient = new HttpClient();
      
        var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetClientSiteByName?name={Uri.EscapeDataString(name)}";

        var response = await httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ClientSite>();
        }

        return null;
    }



    private void ChkColourCodeAlert_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value) // Checked
        {
            DropdownBorder.IsEnabled = true;
            SelectedColourLabel.BackgroundColor = Colors.Transparent; // Normal background
            if (ColourCodeList != null && ColourCodeList.Count > 0)
            {
                var defaultItem = ColourCodeList[0];
                SelectedColourLabel.Text = defaultItem.TemplateName;
                SelectedColourLabel.TextColor = Color.FromArgb(defaultItem.TextColor);
            }

        }
        else // Unchecked
        {
            DropdownBorder.IsEnabled = false;
            ColourCodeDropdown.IsVisible = false; // Hide dropdown if open
            SelectedColourLabel.Text = string.Empty; // Optionally clear selection

            SelectedColourLabel.BackgroundColor = Colors.LightGray;  // Disabled background color

            // Reset label to default text and default color
            if (ColourCodeList != null && ColourCodeList.Count > 0)
            {
                var defaultItem = ColourCodeList[0];
                SelectedColourLabel.Text = defaultItem.TemplateName;
                SelectedColourLabel.TextColor = Color.FromArgb(defaultItem.TextColor);
            }
        }
        // Automatically select "General" if available
        if (TemplateTypesList.Contains("General"))
        {
            templateTypesPicker.SelectedItem = "General";
            LoadTemplatesByType("General");
        }
    }
    private async void OnHomeClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage();
    }


    private async Task LoadFeedbackTemplates()
    {
        try
        {
            var httpClient = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetFeedbackTemplates";

            var templates = await httpClient.GetFromJsonAsync<List<FeedbackTemplateViewModel>>(url);

            if (templates != null)
            {
                _feedbackTemplates = templates;


                var colourCodes = _feedbackTemplates.Where(t => t.Type == 3)
                .OrderBy(t => t.TemplateName).ToList();



                // Add default/placeholder at the beginning
                colourCodes.Insert(0, new FeedbackTemplateViewModel
                {
                    TemplateName = "Select AS.3745 and AS4083 Template",
                    TextColor = "#000000",
                    BackgroundColour = "#FFFFFF"
                });

                ColourCodeList.Clear();
                foreach (var item in colourCodes)
                {
                    ColourCodeList.Add(item);
                }
                // Set default label text
                SelectedColourLabel.BackgroundColor = Colors.LightGray;  // Disabled background color
                SelectedColourLabel.Text = colourCodes[0].TemplateName;
                SelectedColourLabel.TextColor = Color.FromArgb(colourCodes[0].TextColor);
                //ColourCodeCollection.ItemsSource = ColourCodeList;


                var distinctTypes = templates
                .Where(t => !string.IsNullOrEmpty(t.FeedbackTypeName))
                .Select(t => t.FeedbackTypeName)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

                // Add placeholder
                TemplateTypesList.Clear();
                //TemplateTypesList.Add("Select Template Type");
                foreach (var type in distinctTypes)
                    TemplateTypesList.Add(type);

                // Automatically select "General"
                if (TemplateTypesList.Contains("General"))
                {
                    templateTypesPicker.SelectedItem = "General";
                    LoadTemplatesByType("General");
                }

                // Set default for FilteredTemplatesList

            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load templates: {ex.Message}", "OK");
        }

    }

    private void LoadTemplatesByType(string type)
    {
        var filtered = _feedbackTemplates
            .Where(t => t.FeedbackTypeName == type)
            .OrderBy(t => t.TemplateName)
            .ToList();

        FilteredTemplatesList.Clear();

        // Add default placeholder template
        FilteredTemplatesList.Add(new FeedbackTemplateViewModel
        {
            TemplateId = 0,
            TemplateName = "Select Template"
        });

        foreach (var item in filtered)
            FilteredTemplatesList.Add(item);
    }


    private void OnTemplateSelected(object sender, EventArgs e)
    {
        if (templatesPicker.SelectedItem is FeedbackTemplateViewModel selectedTemplate &&
            selectedTemplate.TemplateId != 0)
        {
            // Do something with the selected template
        }
    }

    //private void OnColourCodeSelected(object sender, SelectionChangedEventArgs e)
    //{
    //    if (e.CurrentSelection.FirstOrDefault() is FeedbackTemplateViewModel selectedTemplate)
    //    {
    //        // Do something with selectedTemplate
    //        Console.WriteLine($"Selected: {selectedTemplate.TemplateName}");
    //    }
    //}


    private void ToggleDropdown()
    {
        if (!DropdownBorder.IsEnabled)
            return; // Ignore taps if disabled

        ColourCodeDropdown.IsVisible = !ColourCodeDropdown.IsVisible;
    }

    private void OnColourCodeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FeedbackTemplateViewModel selectedItem)
        {
            SelectedColourLabel.Text = selectedItem.TemplateName;
            SelectedColourLabel.TextColor = Color.FromArgb(selectedItem.TextColor);
            SelectedColourLabel.BackgroundColor = Color.FromArgb(selectedItem.BackgroundColour);
            ColourCodeDropdown.IsVisible = false;


            // Directly load templates by FeedbackTypeName


            // Automatically select "General" if available
            if (TemplateTypesList.Contains(selectedItem.FeedbackTypeName))
            {
                templateTypesPicker.SelectedItem = selectedItem.FeedbackTypeName;
                LoadTemplatesByType(selectedItem.FeedbackTypeName);
            }

            // Select the exact template in Templates dropdown
            var matchingTemplate = FilteredTemplatesList
                .FirstOrDefault(t => t.TemplateId == selectedItem.TemplateId);
            if (matchingTemplate != null)
            {
                templatesPicker.SelectedItem = matchingTemplate;

                // Optionally populate description
                descriptionEditor.Text = matchingTemplate.Text;
            }
        }
    }





    private void OnDropdownTapped(object sender, EventArgs e)
    {
        ToggleDropdown();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        try
        {

            // Retrieve stored values from SecureStorage
            var guardId = await SecureStorage.GetAsync("GuardId");
            var guardName = await SecureStorage.GetAsync("GuardName");
            var savedLicenseNumber = await SecureStorage.GetAsync("LicenseNumber");
            var clientSite = await SecureStorage.GetAsync("ClientSite");
            string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");
            var clientSiteId = await SecureStorage.GetAsync("SelectedClientSiteId");

           




            var clientType = await SecureStorage.GetAsync("ClientType");
            string clientTypeNew = Regex.Replace(clientType, @"\s*\(\d+\)$", "").Trim();
            // Validate required values before proceeding
            if (string.IsNullOrWhiteSpace(guardId) ||
                string.IsNullOrWhiteSpace(guardName) ||
                string.IsNullOrWhiteSpace(savedLicenseNumber) ||
                string.IsNullOrWhiteSpace(clientSite) ||
                string.IsNullOrWhiteSpace(clientTypeNew))
            {
                await DisplayAlert("Missing Info", "Some required session values are missing. Please log in again or contact support.", "OK");
                return;
            }

            // 1. Check if at least one EventType is selected
            if (!(chkHrRelated.IsChecked ||
                  chkOhsMatters.IsChecked ||
                  chkSecurityBreach.IsChecked ||
                  chkEquipmentDamage.IsChecked ||
                  chkCctvRelated.IsChecked ||
                  chkEmergencyServices.IsChecked ||
                  chkColourCodeAlert.IsChecked ||
                  chkHealthRestraints.IsChecked ||
                  chkGeneralPatrol.IsChecked ||
                  chkAlarmActive.IsChecked ||
                  chkAlarmDisabled.IsChecked ||
                  chkClientOnsite.IsChecked ||
                  chkEquipmentCarried.IsChecked ||
                  chkOtherCategories.IsChecked))
            {
                await DisplayAlert("Validation Error", "Please select at least one event type.", "OK");
                return;
            }

            // 2. Validate Wand Scanned selection
            if (!(rbYes.IsChecked || rbNo.IsChecked))
            {
                await DisplayAlert("Validation Error", "Please select Does the BodyCamera, DashCAM, or other video footage part of this IR.", "OK");
                return;
            }

            // 3. Client Type check
            if (string.IsNullOrWhiteSpace(clientTypeNew))
            {
                await DisplayAlert("Validation Error", "Client Type is required.", "OK");
                return;
            }

            // 4. Client Site check
            if (string.IsNullOrWhiteSpace(clientSite))
            {
                await DisplayAlert("Validation Error", "Client Site is required.", "OK");
                return;
            }

            // 5. Guard Month or Year on Site check
            if (GuardMonthPicker.SelectedItem == null || string.IsNullOrWhiteSpace(GuardMonthPicker.SelectedItem.ToString()))
            {
                await DisplayAlert("Validation Error", "Guard months or years on site is required.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(descriptionEditor?.Text))
            {
                await DisplayAlert("Validation Error", "You cannot submit an IR without any Text or Information on the notes section.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(descriptionEditor?.Text))
            {
                await DisplayAlert("Validation Error", "You cannot submit an IR without any Text or Information on the notes section.", "OK");
                return;
            }




         

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/ProcessIrSubmit?IRguardId={guardId}&IRclientSiteId={clientSiteId}";
            
            var selectedColourCode = ColourCodeDropdown.SelectedItem as FeedbackTemplateViewModel;

            int? selectedColourCodeId = selectedColourCode?.TemplateId;

            DateTime reportDateTime = new DateTime();

           
          
                reportDateTime = new DateTime(
                    reportDatePickerOffsite.Date.Year,
                    reportDatePickerOffsite.Date.Month,
                    reportDatePickerOffsite.Date.Day,
                    reportTimePickerOffsite.Time.Hours,
                    reportTimePickerOffsite.Time.Minutes,
                    0
                );
           

            if (_currentClientSite == null)
            {
                await DisplayAlert("Missing Info", "Client site information is missing. Please try again.", "OK");
                return;
            }

            var selectedGuardMonth = GuardMonthPicker.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedGuardMonth))
            {
                await DisplayAlert("Validation Error", "Please select the guard's months or years on site.", "OK");
                return;
            }

            if (chkColourCodeAlert?.IsChecked == true && ColourCodeDropdown.SelectedItem == null)
            {
                await DisplayAlert("Validation Error", "Please select a Colour Code if 'COLOUR Code Alert' is checked.", "OK");
                return;
            }

            if (uploadedServerFileNames == null || uploadedServerFileNames.Count == 0)
            {
                bool confirm = await DisplayAlert(
                    "No Attachments",
                    "Are you sure you want to submit this IR without a photo to support your claim or observations?",
                    "Yes", "No");

                if (!confirm)
                    return; // Stop submission
            }


            LoadingOverlay.IsVisible = true; // Show loader
            var selectedNotifiedBy = NotifiedByPicker.SelectedItem as string ?? string.Empty;

            string clientArea = null;

            if (areaPicker != null &&
                areaPicker.ItemsSource is IEnumerable<AreaItem> source &&
                source.Any() &&
                areaPicker.SelectedItem is AreaItem selectedArea &&
                !string.IsNullOrWhiteSpace(selectedArea.Value) &&
                !selectedArea.Value.Equals("Select", StringComparison.OrdinalIgnoreCase))
            {
                clientArea = selectedArea.Value;
            }


            var Report = new IncidentRequest
            {
                ReportReference= IrSession.ReportReference,
               
                EventType = new EventType
                {
                    HrRelated = chkHrRelated?.IsChecked ?? false,
                    OhsMatters = chkOhsMatters?.IsChecked ?? false,
                    SecurtyBreach = chkSecurityBreach?.IsChecked ?? false,
                    EquipmentDamage = chkEquipmentDamage?.IsChecked ?? false,
                    Thermal = chkCctvRelated?.IsChecked ?? false,
                    Emergency = chkEmergencyServices?.IsChecked ?? false,
                    SiteColour = chkColourCodeAlert?.IsChecked ?? false,
                    HealthDepart = chkHealthRestraints?.IsChecked ?? false,
                    GeneralSecurity = chkGeneralPatrol?.IsChecked ?? false,
                    AlarmActive = chkAlarmActive?.IsChecked ?? false,
                    AlarmDisabled = chkAlarmDisabled?.IsChecked ?? false,
                    AuthorisedPerson = chkClientOnsite?.IsChecked ?? false,
                    Equipment = chkEquipmentCarried?.IsChecked ?? false,
                    Other = chkOtherCategories?.IsChecked ?? false
                },
                SiteColourCode = selectedColourCode?.Text ?? string.Empty,
                SiteColourCodeId = selectedColourCode?.TemplateId,
                WandScannedYes3a = false,
                WandScannedYes3b = false,
                WandScannedNo = true,
                BodyCameraYes = rbYes?.IsChecked ?? false,
                BodyCameraNo = rbNo?.IsChecked ?? false,
                Officer = new Officer
                {
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    Gender = string.Empty,
                    Phone = string.Empty,
                    Position = string.Empty,
                    Email = "username@example.com",
                    LicenseNumber = string.Empty,
                    LicenseState = string.Empty,
                    CallSign = string.Empty,
                    GuardMonth = selectedGuardMonth,
                    NotifiedBy = selectedNotifiedBy,
                    Billing = string.Empty,
                },
                IsPositionPatrolCar = false,
                DateLocation = new DateLocation
                {

                    IncidentDate = enableDateTimeCheckBox?.IsChecked == true
    ? new DateTime(
        incidentDatePicker.Date.Year,
        incidentDatePicker.Date.Month,
        incidentDatePicker.Date.Day,
        incidentTimePicker.Time.Hours,
        incidentTimePicker.Time.Minutes,
        0)
    : (DateTime?)null,
                    //IncidentDate = new DateTime(
                    //    incidentDatePicker.Date.Year,
                    //    incidentDatePicker.Date.Month,
                    //    incidentDatePicker.Date.Day,
                    //    incidentTimePicker.Time.Hours,
                    //    incidentTimePicker.Time.Minutes,
                    //    0
                    //),
                    ReportDate = reportDateTime,
                    ReimbursementNo = reimbursementNoCheckBox?.IsChecked ?? false,
                    ReimbursementYes = reimbursementYesCheckBox?.IsChecked ?? false,
                    JobNumber = JobNumberEntry?.Text ?? string.Empty,
                    JobTime = null,
                    Duration = null,
                    Travel = 0,
                    PatrolExternal = PatrolExternalCheckBox?.IsChecked ?? false,
                    PatrolInternal = PatrolInternalCheckBox?.IsChecked ?? false,
                    ClientType = clientTypeNew ?? string.Empty,
                    ClientSite = clientSite ?? string.Empty,
                    ClientArea = clientArea,
                    ShowIncidentLocationAddress = incidentLocationCheckBox?.IsChecked ?? false,
                    ClientAddress = clientAddressEntry?.Text ?? string.Empty,
                    State = _currentClientSite.State ?? string.Empty,
                    ClientStatus = _currentClientSite?.Status ?? 0,
                    ClientSiteLiveGps = _currentClientSite.Gps ?? string.Empty,
                },
                LinkedSerialNos = null,
                Feedback = descriptionEditor?.Text ?? string.Empty,
                ReportedBy = null,
                FeedbackType = _selectedTemplate?.Type ?? 0,
                FeedbackTemplates = _selectedTemplate?.TemplateId ?? 0,
                PSPFName = "[SEC=UNOFFICIAL]"
            };


            Report.Attachments = uploadedServerFileNames;
            //var report = new IncidentRequest
            //{
            //    EventType = new EventType
            //    {
            //        HrRelated = true
            //    },
            //    SiteColourCodeId = 1,
            //    WandScannedYes3a = true,
            //    WandScannedYes3b = true,
            //    WandScannedNo = true,
            //    BodyCameraYes = true,
            //    BodyCameraNo = false,
            //    Officer = new Officer
            //    {
            //        FirstName = "Dileep",
            //        LastName = "Seb",
            //        Gender = "Male",
            //        Position = "SG01",
            //        GuardMonth = "Apri"  
            //    },
            //    DateLocation = new DateLocation
            //    {
            //        JobNumber = "1234",
            //        ClientSite = "ABC Corp",    
            //        ClientType = "Construction" 
            //    },
            //    ReportedBy = "Supervisor Name",
            //};


            // Send the object as JSON and get the response as a strongly-typed list
            //using var httpClient = new HttpClient();
            //var response = await httpClient.PostAsJsonAsync(url, Report);
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync("", Report);

            var result = await response.Content.ReadFromJsonAsync<ProcessIrResponse>();

            if (!string.IsNullOrEmpty(result?.FileName))
            {
                var staticBaseUrl = "https://cws-ir.com/Pdf/ToDropbox/";
                var fullDownloadUrl = $"{staticBaseUrl}{Uri.EscapeDataString(result.FileName)}";

                Application.Current.MainPage = new NavigationPage(new DownloadIr(fullDownloadUrl));
               
            }
            else
            {
                var errorMessages = string.Join("\n", result?.Errors.Select(e => $"{e.Code}: {e.Message}"));
                await DisplayAlert("Error", $"Failed to generate report:\n{errorMessages}", "OK");
            }
            // Use 'templates' as needed
            IrSession.ReportReference = Guid.NewGuid().ToString();
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {response.StatusCode}, Details: {error}");
            await DisplayAlert("Success", "Incident submitted successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Exception", ex.Message, "OK");
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private void OnTemplateTypeChanged(object sender, EventArgs e)
    {
        _selectedFeedbackType = templateTypesPicker.SelectedItem as string;

        if (!string.IsNullOrEmpty(_selectedFeedbackType) && _selectedFeedbackType != "Select Template Type")
        {
            var matchingTemplates = _feedbackTemplates
                .Where(t => t.FeedbackTypeName == _selectedFeedbackType)
                .OrderBy(t => t.TemplateName)
                .ToList();

            FilteredTemplatesList.Clear();
            foreach (var template in matchingTemplates)
                FilteredTemplatesList.Add(template);
        }
        else
        {
            FilteredTemplatesList.Clear();
            _selectedFeedbackType = null; // reset
        }
    }

    private void templatesPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (templatesPicker.SelectedItem is FeedbackTemplateViewModel selectedTemplate &&
            selectedTemplate.TemplateId != 0)
        {
            _selectedTemplate = selectedTemplate;
            descriptionEditor.Text = selectedTemplate.Text;
        }
        else
        {
            _selectedTemplate = null;
            descriptionEditor.Text = string.Empty;
        }
    }


    private void OnClientAddressTextChanged(object sender, TextChangedEventArgs e)
    {
       
           ClientAddress = e.NewTextValue;
        
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {

        if (
            e.CurrentSelection.FirstOrDefault() is string selectedAddress)
        {
            ClientAddress = selectedAddress;
            IsSuggestionsVisible = false;
            Suggestions.Clear(); // 

           
        }

    ((CollectionView)sender).SelectedItem = null;
    }
    private void OnIncidentLocationCheckBoxChanged(object sender, CheckedChangedEventArgs e)
    {
      
            if (e.Value) // Checkbox is checked
            {
                _savedClientAddress = ClientAddress;
                ClientAddress = string.Empty;
               // vm.IsSuggestionsVisible = true;
                IsSearchEnabled = true;
            }
            else // Checkbox is unchecked
            {
                IsSearchEnabled = false;
                IsSuggestionsVisible = false;
                ClientAddress = _savedClientAddress;
                Suggestions.Clear();
            }
        
    }



    public class ProcessIrResponse
    {
        public bool Success { get; set; }
        public string FileName { get; set; }
        public List<ProcessIrError> Errors { get; set; }
    }

    public class ProcessIrError
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }

    public class ClientSite
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
        public string Gps { get; set; }
        public string Billing { get; set; }
        public int Status { get; set; }
        public DateTime? StatusDate { get; set; }
        public string SiteEmail { get; set; }
        public string LandLine { get; set; }
        public string DuressEmail { get; set; }
        public string DuressSms { get; set; }
        public bool UploadGuardLog { get; set; }
        public bool UploadFusionLog { get; set; }
        public string GuardLogEmailTo { get; set; }
        public bool DataCollectionEnabled { get; set; }
        public bool IsActive { get; set; }
        public bool IsDosDontList { get; set; }
    }
    public class FeedbackTemplateViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Text { get; set; }
        public int? Type { get; set; }
        public string FeedbackTypeName { get; set; }
        public string BackgroundColour { get; set; }
        public string TextColor { get; set; }
        public int DeleteStatus { get; set; }
        public bool SendtoRC { get; set; }
    }

    //private async void OnUploadAttachmentClicked(object sender, EventArgs e)
    //{
    //    try
    //    {
    //        var results = await FilePicker.PickMultipleAsync();

    //        if (results == null)
    //            return;

    //        var filesToSave = results
    //            .Where(f => allowedExtensions.Contains(Path.GetExtension(f.FileName).ToLower()))
    //            .ToList();

    //        if (!filesToSave.Any())
    //        {
    //            await DisplayAlert("No Valid Files", "Please select supported file types.", "OK");
    //            return;
    //        }

    //        foreach (var file in filesToSave)
    //        {
    //            var stream = await file.OpenReadAsync();
    //            var localFilePath = Path.Combine(FileSystem.CacheDirectory, file.FileName);

    //            // Save to local cache
    //            using (var fileStream = File.Create(localFilePath))
    //            {
    //                await stream.CopyToAsync(fileStream);
    //            }
    //            string reportReference = IrSession.ReportReference;
    //            // Upload to server and get uploaded filename
    //            var uploadedFileName = await UploadFileToServer(localFilePath, reportReference);

    //            if (!string.IsNullOrWhiteSpace(uploadedFileName) && !UploadedFiles.Contains(uploadedFileName))
    //            {
    //                UploadedFiles.Add(uploadedFileName); // Display to UI
    //                uploadedServerFileNames.Add(uploadedFileName); // For sending in report
    //            }
    //        }

    //        uploadDisplaySection.IsVisible = UploadedFiles.Any();
    //    }
    //    catch (Exception ex)
    //    {
    //        await DisplayAlert("Error", $"File selection failed: {ex.Message}", "OK");
    //    }
    //}


    private async void OnUploadAttachmentClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.PickMultipleAsync();
            if (results == null) return;

            var filesToSave = results
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f.FileName).ToLower()))
                .ToList();

            if (!filesToSave.Any())
            {
                await DisplayAlert("No Valid Files", "Please select supported file types.", "OK");
                return;
            }

            uploadProgressBar.IsVisible = true;
            uploadProgressBar.Progress = 0;

            int totalFiles = filesToSave.Count;
            int uploadedCount = 0;

            foreach (var file in filesToSave)
            {
                var stream = await file.OpenReadAsync();
                var localFilePath = Path.Combine(FileSystem.CacheDirectory, file.FileName);

                using (var fileStream = File.Create(localFilePath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                string reportReference = IrSession.ReportReference;
                var uploadedFileName = await UploadFileToServer(localFilePath, reportReference);

                if (!string.IsNullOrWhiteSpace(uploadedFileName) && !UploadedFiles.Contains(uploadedFileName))
                {
                    UploadedFiles.Add(uploadedFileName);
                    uploadedServerFileNames.Add(uploadedFileName);
                }

                uploadedCount++;
                uploadProgressBar.Progress = (double)uploadedCount / totalFiles;
            }

            uploadProgressBar.IsVisible = false;

            // Show uploaded list
            uploadDisplaySection.IsVisible = UploadedFiles.Any();
        }
        catch (Exception ex)
        {
            uploadProgressBar.IsVisible = false;
            await DisplayAlert("Error", $"File selection failed: {ex.Message}", "OK");
        }
    }

    private void OnRemoveFileClicked(object sender, EventArgs e)
    {
        var button = sender as ImageButton;
        var fileName = button?.CommandParameter as string;

        if (!string.IsNullOrEmpty(fileName))
        {
            UploadedFiles.Remove(fileName);
            uploadedServerFileNames.Remove(fileName);
        }
        uploadDisplaySection.IsVisible = UploadedFiles.Any();
    }



    private async Task<string> UploadFileToServer(string localFilePath, string reportReference)
    {
        var fileName = Path.GetFileName(localFilePath);
        var httpClient = new HttpClient();

        using var fileStream = File.OpenRead(localFilePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        content.Add(fileContent, "file", fileName);

        // Attach reportReference to the URL
        var uploadUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadFile?reportReference={reportReference}";

        var response = await httpClient.PostAsync(uploadUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
        return result?.FileName;
    }


    public class FileUploadResponse
    {
        public string FileName { get; set; }
    }


    private void OnEnableDateTimeCheckBoxChanged(object sender, CheckedChangedEventArgs e)
    {
        bool isEnabled = e.Value;

        dateTimePickerLayout.IsVisible = isEnabled;
        disabledDateTimeLabel.IsVisible = !isEnabled;
    }

    private async Task LoadClientAreas(int clientSiteId)
    {
        try
        {
            using var httpClient = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/areas?clientSiteId={clientSiteId}";
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var areaItems = JsonSerializer.Deserialize<List<AreaItem>>(json, options);

                if (areaItems != null)
                {
                    // Remove empty-value items
                    var actualItems = areaItems
                        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                        .ToList();

                    if (actualItems.Any())
                    {
                        // Add "Select" as first item manually
                        actualItems.Insert(0, new AreaItem
                        {
                            Text = "Select",
                            Value = "",
                            Selected = true
                        });

                        areaPicker.ItemsSource = actualItems;
                        areaPicker.ItemDisplayBinding = new Binding("Text");
                        areaPicker.SelectedIndex = 0;
                    }
                    // else: do NOT bind the picker at all
                }
            }
            else
            {
                await DisplayAlert("Error", "Unable to load area list from server.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Exception loading area list: {ex.Message}", "OK");
        }
    }

    public class AreaDto
    {
        public string Text { get; set; }
        public string Value { get; set; }
        public bool Selected { get; set; }
    }

    public class AreaItem
    {
        public string Text { get; set; }
        public string Value { get; set; }
        public bool Selected { get; set; }
    }
}


public class GooglePlacesAutocompletePrediction
{
    public string Description { get; set; }
    public string PlaceId { get; set; }
}




// ---------- Google Places Service ----------
public class GooglePlacesService
{
    private readonly string _apiKey = "AIzaSyCJK5DRhsD9rePFC-p9_8schzBdZsmXfUs"; // <-- Replace with your actual API key

    public async Task<List<string>> GetSuggestionsAsync(string input)
    {
        
        var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(input)}&types=address&components=country:au&key={_apiKey}";
        using var client = new HttpClient();
        var json = await client.GetStringAsync(url);

        var result = JsonSerializer.Deserialize<GooglePlacesAutocompleteResponse>(json);
        return result?.Predictions?.Select(p => p.Description).ToList() ?? new List<string>();
    }
}

// ---------- Google API DTOs ----------
public class GooglePlacesAutocompleteResponse
{
    [JsonPropertyName("predictions")]
    public List<Prediction> Predictions { get; set; }
}

public class Prediction
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("place_id")]
    public string PlaceId { get; set; }

    [JsonPropertyName("structured_formatting")]
    public StructuredFormatting StructuredFormatting { get; set; }

    [JsonPropertyName("terms")]
    public List<Term> Terms { get; set; }

    [JsonPropertyName("types")]
    public List<string> Types { get; set; }
}

public class StructuredFormatting
{
    [JsonPropertyName("main_text")]
    public string MainText { get; set; }

    [JsonPropertyName("secondary_text")]
    public string SecondaryText { get; set; }
}

public class Term
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}
