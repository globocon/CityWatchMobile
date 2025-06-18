using C4iSytemsMobApp.Interface;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class WebIncidentReport : ContentPage
{

    private List<FeedbackTemplateViewModel> _feedbackTemplates = new();
    public ObservableCollection<FeedbackTemplateViewModel> ColourCodeList { get; set; } = new();
    public ObservableCollection<string> TemplateTypesList { get; set; } = new();
    public ObservableCollection<FeedbackTemplateViewModel> FilteredTemplatesList { get; set; } = new();
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFeedbackTemplates();
    }
    public WebIncidentReport()
    {
        InitializeComponent();
        BindingContext = this; // or to your ViewModel
        reportDatePickerOffsite.Date = DateTime.Today;
        reportTimePickerOffsite.Time = DateTime.Now.TimeOfDay; // Set default time


        // Initially disable tap gesture (will be enabled when checkbox is checked)
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => ToggleDropdown();
        DropdownBorder.GestureRecognizers.Add(tapGesture);
        DropdownBorder.IsEnabled = false; // Initially disabled

        

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
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
        //Application.Current.MainPage = new MainPage();
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
        // Replace with your API submission logic
        await DisplayAlert("Submitted", "Your data has been submitted successfully.", "OK");
    }

    private void OnTemplateTypeChanged(object sender, EventArgs e)
    {
        var selectedType = templateTypesPicker.SelectedItem as string;

        if (!string.IsNullOrEmpty(selectedType) && selectedType != "Select Template Type")
        {
            var matchingTemplates = _feedbackTemplates
                .Where(t => t.FeedbackTypeName == selectedType)
                .OrderBy(t => t.TemplateName)
                .ToList();

            FilteredTemplatesList.Clear();
            foreach (var template in matchingTemplates)
                FilteredTemplatesList.Add(template);
        }
        else
        {
            FilteredTemplatesList.Clear();
        }
    }

    private void templatesPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (templatesPicker.SelectedItem is FeedbackTemplateViewModel selectedTemplate &&
            selectedTemplate.TemplateId != 0) // Skip the placeholder
        {
            descriptionEditor.Text = selectedTemplate.Text;
        }
        else
        {
            descriptionEditor.Text = string.Empty;
        }
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
}