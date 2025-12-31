using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace C4iSytemsMobApp;


public partial class HrRecordsPage : ContentPage
{
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    private readonly IGuardApiServices _guardApiServices;
    public ObservableCollection<GuardComplianceAndLicense> HrRecords { get; set; } = new();

    private bool IsFilesVisible = false;
    private string FileName = string.Empty;
    public ObservableCollection<MyFileModel> SelectedFiles { get; set; } = new();
    public ObservableCollection<HRGroups> HrGroupsList { get; set; } = new();
    public ObservableCollection<CombinedData> HrDescriptionList { get; set; } = new();
    public IssueExpiryDateViewModel IssueExpiryVM { get; set; }
    private string savedLicenseNumber = string.Empty;
    private bool IsNew = true;

    public HrRecordsPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        IssueExpiryVM = new IssueExpiryDateViewModel();
        IssueExpiryVM.IsExpiry = true;
        BindingContext = this;
        _guardApiServices = IPlatformApplication.Current.Services.GetService<IGuardApiServices>();
        EditCommand = new Command<GuardComplianceAndLicense>(OnEditSwiped);

        //EditCommand = new Command<PatrolCarLog>(OnEdit);
        DeleteCommand = new Command<GuardComplianceAndLicense>(OnDeleteSwiped);
        savedLicenseNumber = Preferences.Get("LicenseNumber", "");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHrRecordsAsync();
        await LoadHrGroupsListAsync();
    }

    private async Task LoadHrRecordsAsync()
    {

        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;

            // Clear old data
            HrRecords.Clear();

            var rows = await _guardApiServices.GetHrRecordsOfGuard();
            if (rows != null && rows.Count > 0 && rows.Any())
            {

                foreach (var log in rows.OrderBy(x => x.HrGroup).ThenBy(x => x.Description))
                {
                    HrRecords.Add(log);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load Hr documents: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.IsVisible = false;
        }
    }

    private async Task LoadHrGroupsListAsync()
    {

        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;

            // Clear old data
            HrGroupsList.Clear();

            var rows = await _guardApiServices.GetHrGroups();
            if (rows != null && rows.Count > 0 && rows.Any())
            {
                foreach (var group in rows)
                {
                    HrGroupsList.Add(group);
                }
            }

            // OnPropertyChanged(nameof(HrGroupsList));
        }
        catch (Exception ex)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.IsVisible = false;
        }
    }

    private async Task LoadHrGroupsDescriptionsListAsync(int hrGroupId)
    {

        try
        {
            // Show loading overlay
            LoadingOverlay.IsVisible = true;

            // Clear old data
            HrDescriptionList.Clear();

            var rows = await _guardApiServices.GetHrGroupDescriptions(hrGroupId);
            if (rows != null && rows.Count > 0 && rows.Any())
            {
                foreach (var log in rows)
                {
                    HrDescriptionList.Add(log);
                }
            }
        }
        catch (Exception ex)
        {
            //await DisplayAlert("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.IsVisible = false;
        }
    }

    private async void OnDeleteSwiped(GuardComplianceAndLicense item)
    {
        if (item == null)
            return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Confirm Delete",
            "Are you sure you want to delete this record?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        try
        {
            IsBusy = true;

            (bool isDeleted, string msg) = await _guardApiServices.DeleteHrDocument(item.Id);

            if (isDeleted)
            {
                HrRecords.Remove(item); // ObservableCollection
                await Application.Current.MainPage.DisplayAlert(
                    "Success",
                    "The record has been deleted successfully.",
                    "OK");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Unable to delete the record.\n{msg}.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(
                "Error",
                ex.Message,
                "OK");
        }
        finally
        {
            IsBusy = false;
        }

    }

    private async void OnEditSwiped(GuardComplianceAndLicense item)
    {
        IsNew = false;

        HrGroupPicker.SelectedItem = HrGroupsList.FirstOrDefault(x => x.Id == (int)item.HrGroup.Value);
        var selectedHrGroup = HrGroupPicker.SelectedItem as HRGroups;
        var selectedId = selectedHrGroup.Id;
        await LoadHrGroupsDescriptionsListAsync(selectedId);
        DescriptionGroupPicker.SelectedItem = HrDescriptionList.FirstOrDefault(x => x.ReferenceNoAndDescription == item.Description);
        IssueExpiryVM.IsExpiry = item.DateType ? !item.DateType : true;
        IssueExpiryVM.DisplayDate = item.ExpiryDate.HasValue ? item.ExpiryDate.Value : DateTime.Today;

        //MyFileModel editfile = new MyFileModel
        //{
        //    File = null,
        //    IsNew = false,
        //    FileName = item.FileName
        //};

        //FilesCollectionEditImage.SelectedItem = null;
        //FilesCollectionEditImage.ItemsSource =

        HrGroupPicker.IsEnabled = false;
        DescriptionGroupPicker.IsEnabled = false;
        PopupOverlayAddCompliance.IsVisible = true;
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

    private async void OnAddComplianceClicked(object sender, TappedEventArgs e)
    {
        IsNew = true;

        // Bind to CollectionView        
        IssueExpiryVM.DisplayDate = DateTime.Today;
        IssueExpiryVM.IsExpiry = true;
        FilesCollectionEditImage.ItemsSource = SelectedFiles;
        IsFilesVisible = SelectedFiles.Any();
        FilesCollectionEditImage.IsVisible = IsFilesVisible;
        HrGroupPicker.IsEnabled = true;
        HrGroupPicker.SelectedIndex = -1;
        HrDescriptionList.Clear();
        DescriptionGroupPicker.IsEnabled = true;
        PopupOverlayAddCompliance.IsVisible = true;
    }

    private async void OnHrRecordClicked(object sender, TappedEventArgs e)
    {
        string fileName = string.Empty;
        if (sender is Frame frame && frame.BindingContext != null)
        {
            var item = frame.BindingContext as GuardComplianceAndLicense; // replace with your actual model

            if (item != null)
            {
                fileName = item.FileName;

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    //"/Uploads/Guards/License/4068435/01c_Licence - Private Agent-exp 20 MAY 26.png"



                    var encodedBlobName = Uri.EscapeDataString(fileName);
                    var fileUrl = $"{AppConfig.MobileSignalRBaseUrl}/Uploads/Guards/License/{savedLicenseNumber}/{fileName}";

                    if (!string.IsNullOrWhiteSpace(fileUrl))
                    {
                        try
                        {
                            await Browser.Default.OpenAsync(fileUrl, BrowserLaunchMode.SystemPreferred);
                        }
                        catch
                        {
                            await DisplayAlert("Error", "Unable to open document file.", "OK");
                        }
                    }
                }
            }
        }
    }

    private async void OnHrGroupSelectionChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker)
            return;

        if (picker.SelectedItem == null)
            return;

        var selectedHrGroup = picker.SelectedItem as HRGroups;

        if (selectedHrGroup == null)
            return;

        // Example usage
        var selectedId = selectedHrGroup.Id;
        var selectedName = selectedHrGroup.ToString();
        await LoadHrGroupsDescriptionsListAsync(selectedId);
    }

    private void OnDescriptionGroupSelectionChanged(object sender, EventArgs e)
    {

    }

    private async void OnPickComplianceDocumentClicked(object sender, EventArgs e)
    {
        if (SelectedFiles.Any())
        {
            await DisplayAlert("Error", "Only one file is allowed. Please delete the current file to add a new file.", "OK");
            return;
        }

        try
        {
            var file = await FilePicker.PickAsync(); // Allow single file selection
            if (file != null)
            {
                // Allowed image formats
                string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".png", ".pdf" };


                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    await DisplayAlert("Invalid File", $"File '{file.FileName}' is not a supported image type.", "OK");
                    return;
                }

                SelectedFiles.Add(new MyFileModel
                {
                    File = file,
                    IsNew = true,
                });
            }

            // Show the file list only if it has items
            IsFilesVisible = SelectedFiles.Any();
            FilesCollectionEditImage.IsVisible = IsFilesVisible;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
        }
    }

    private void OnDeleteFileClicked(object sender, EventArgs e)
    {
        SelectedFiles.Clear();
    }


    private void OnClosePopupClicked(object sender, TappedEventArgs e)
    {
        PopupOverlayAddCompliance.IsVisible = false;
        // Clear the edit file list
        if (FilesCollectionEditImage.ItemsSource is ObservableCollection<MyFileModel> files)
        {
            files.Clear();
        }
        SelectedFiles.Clear();

        // Hide the file list view
        FilesCollectionEditImage.IsVisible = false;
    }

    private async void OnEditSaveComplianceClicked(object sender, TappedEventArgs e)
    {
        // Check if HrGroup and Description are selected
        // Check if Issue Or Expiry Date are valid
        // Check if a file is selected

        var hrGroupSelected = HrGroupPicker.SelectedItem as HRGroups;
        var descriptionSelected = DescriptionGroupPicker.SelectedItem as CombinedData;
        var isFileSelected = SelectedFiles.Any();
        var DescriptionId = descriptionSelected != null ? descriptionSelected.ID : 0;

        var IssueOrExpiryDate = IssueExpiryVM.DisplayDate;
        var IsExpiry = IssueExpiryVM.IsExpiry;
        var IsDateValid = false;
        var DateErrorMsg = string.Empty;

        if (IsExpiry)
        {
            IsDateValid = IssueOrExpiryDate.Date >= DateTime.Today.Date;
            DateErrorMsg = "Invalid Expiry date.";
        }
        else
        {
            IsDateValid = IssueOrExpiryDate.Date <= DateTime.Today.Date;
            DateErrorMsg = "Invalid Issue date.";
        }


        if (descriptionSelected == null)
        {
            await DisplayAlert("Error", "Please select a valid description.", "OK");
            return;
        }

        var hrban = await _guardApiServices.CheckForHrBan(DescriptionId);
        if (hrban)
        {
            await DisplayAlert("Error", $"This description is restricted and cannot be added.", "OK");
            return;
        }

        if (hrGroupSelected == null)
        {
            await DisplayAlert("Error", "Please select a valid HR Group.", "OK");
            return;
        }


        if (!IsDateValid)
        {
            await DisplayAlert("Error", DateErrorMsg, "OK");
            return;
        }

        if (!isFileSelected)
        {
            await DisplayAlert("Error", "Please ensure a file is selected.", "OK");
            return;
        }


        GuardComplianceAndLicense guardComplianceAndLicense = new GuardComplianceAndLicense
        {
            HrGroup = (HrGroup)hrGroupSelected.Id,
            HrGroupText = hrGroupSelected.Name,
            Description = descriptionSelected.ReferenceNoAndDescription,
            ExpiryDate = IssueOrExpiryDate,
            DateType = !IsExpiry,
            IsDateFilterEnabledHidden = !IsExpiry,
            LicenseNo = savedLicenseNumber,
            CurrentDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };


        var fullFileName = SelectedFiles.First().File.FileName;
        //var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullFileName);
        var fileExtension = Path.GetExtension(fullFileName);

        if (guardComplianceAndLicense.DateType)
        {
            guardComplianceAndLicense.FileName = $"{descriptionSelected.ReferenceNo}_{descriptionSelected.Description}-doi {guardComplianceAndLicense.ExpiryDate.Value.ToString("dd MMM yy")}{fileExtension}";
        }
        else
        {
            guardComplianceAndLicense.FileName = $"{descriptionSelected.ReferenceNo}_{descriptionSelected.Description}-exp {guardComplianceAndLicense.ExpiryDate.Value.ToString("dd MMM yy")}{fileExtension}";
        }


        var r = await _guardApiServices.SaveHrDocument(guardComplianceAndLicense, SelectedFiles.First().File);
        if (!r)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to save HR Record document.", "OK");
            return;
        }
        else
        {
            await LoadHrRecordsAsync();
            await Application.Current.MainPage.DisplayAlert("Success", "HR Record document has been saved.", "OK");
            // Close popup
            PopupOverlayAddCompliance.IsVisible = false;
            // Clear the edit file list
            if (FilesCollectionEditImage.ItemsSource is ObservableCollection<MyFileModel> files)
            {
                files.Clear();
            }
            SelectedFiles.Clear();
            // Hide the file list view
            FilesCollectionEditImage.IsVisible = false;
        }

    }


}


