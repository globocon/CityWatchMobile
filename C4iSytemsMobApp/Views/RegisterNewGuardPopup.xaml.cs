
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class RegisterNewGuardPopup : Popup
{
    public ObservableCollection<DropdownItem> Genders { get; set; } = new();
    public ObservableCollection<DropdownItem> States { get; set; } = new();
    private readonly IGuardApiServices _guardApiServices;
    private string _guardSecurityNumber;
    private bool initialloadGender = true;
    private bool initialloadState = true;


    public RegisterNewGuardPopup()
    {
        InitializeComponent();
        BindingContext = this;
        _guardApiServices = IPlatformApplication.Current.Services.GetService<IGuardApiServices>();
        PopulateGenders();
        //Task.Run(async () => await PopulateStates());
        PopulateStates();
    }


    private async void OnSaveClicked(object sender, EventArgs e)
    {
        bool emailValid = IsValidEmail(GuardEmail.Text);
        bool mobileValid = IsValidAustralianMobile(GuardMobile.Text);
        bool fullNameValid = IsValidFullName(GuardFullNameEntry.Text);
        bool guardInitialsValid = IsValidInitials(GuardInitials.Text);
        bool guardStateValid = IsValidState(pickerGuardState.SelectedIndex);
        bool guardGenderValid = IsValidGender(pickerGuardGender.SelectedIndex);
        bool securityNumberValid = !SecurityNumberIsNotValid(GuardSecurityNumberEntry.Text);



        FullNameBorder.Stroke = fullNameValid ? Colors.Transparent : Colors.Red;
        FullNameErrorLabel.IsVisible = !fullNameValid;

        GuardInitialsBorder.Stroke = guardInitialsValid ? Colors.Transparent : Colors.Red;
        GuardInitialsErrorLabel.IsVisible = !guardInitialsValid;

        GuardStatesBorder.Stroke = guardStateValid ? Colors.Transparent : Colors.Red;
        GuardStateErrorLabel.IsVisible = !guardStateValid;

        GuardGenderBorder.Stroke = guardGenderValid ? Colors.Transparent : Colors.Red;
        GuardGenderErrorLabel.IsVisible = !guardGenderValid;

        EmailBorder.Stroke = emailValid ? Colors.Transparent : Colors.Red;
        EmailErrorLabel.IsVisible = !emailValid;

        MobileBorder.Stroke = mobileValid ? Colors.Transparent : Colors.Red;
        MobileErrorLabel.IsVisible = !mobileValid;

        SecurityNumberBorder.Stroke = securityNumberValid ? Colors.Transparent : Colors.Red;
        SecurityNumberErrorLabel.IsVisible = !securityNumberValid;

        if (!emailValid || !mobileValid || !fullNameValid || !guardStateValid || !guardStateValid || !securityNumberValid)
            return;

       
        var newGuard = new NewGuard
        {
            Id = 0,
            Name = GuardFullNameEntry.Text,
            SecurityNo = GuardSecurityNumberEntry.Text.Trim(),
            Initial = GuardInitials.Text,
            Gender = (pickerGuardGender.SelectedItem as DropdownItem)?.Name,
            State = (pickerGuardState.SelectedItem as DropdownItem)?.Name,
            Email = GuardEmail.Text,
            Mobile = GuardMobile.Text,
            IsLB_KV_IR = true,
            IsMobileAppAccess = true
        };

        var (isSuccess, errorMessage, newGuardResult) = await _guardApiServices.RegisterNewGuardAsync(newGuard);

        if (!isSuccess)
        {
            await Application.Current.MainPage.DisplayAlert("Error", errorMessage, "OK");
            return;
        }
        else {             
            await Application.Current.MainPage.DisplayAlert("Success", errorMessage, "OK");
            _guardSecurityNumber = newGuard.SecurityNo;
            Close(_guardSecurityNumber);
        }       
    }

    private void OnEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        bool isValid = IsValidEmail(e.NewTextValue);

        EmailBorder.Stroke = isValid ? Colors.Transparent : Colors.Red;
        EmailErrorLabel.IsVisible = !isValid && !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private void OnMobileTextChanged(object sender, TextChangedEventArgs e)
    {
        bool isValid = IsValidAustralianMobile(e.NewTextValue);

        MobileBorder.Stroke = isValid ? Colors.Transparent : Colors.Red;
        MobileErrorLabel.IsVisible = !isValid && !string.IsNullOrWhiteSpace(e.NewTextValue);
    }


    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase);
    }

    private bool IsValidAustralianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
            return false;

        // Remove spaces and hyphens
        mobile = mobile.Replace(" ", "").Replace("-", "");

        // 04XXXXXXXX
        if (Regex.IsMatch(mobile, @"^04\d{8}$"))
            return true;

        // +614XXXXXXXX or 614XXXXXXXX
        if (Regex.IsMatch(mobile, @"^(\+61|61)4\d{8}$"))
            return true;

        return false;
    }

    private bool IsValidFullName(string fullname)
    {
        if (string.IsNullOrWhiteSpace(fullname))
            return false;

        return fullname.Length >= 3;
    }

    private bool IsValidInitials(string initials)
    {
        if (string.IsNullOrWhiteSpace(initials))
            return false;

        return initials.Length >= 2;
    }

    private bool IsValidState(int selectedIdx)
    {
        if (selectedIdx < 1)
            return false;

        return true; // Assuming valid state if selected index is >= 1
    }

    private bool IsValidGender(int selectedIdx)
    {
        if (selectedIdx < 1)
            return false;

        return true; // Assuming valid state if selected index is >= 1
    }

    private void PopulateGenders()
    {
        Genders.Clear();
        List<DropdownItem> dropdownItems = new List<DropdownItem>
        {
            new DropdownItem { Id = -1, Name = "Select" },
            new DropdownItem { Id = 1, Name = "Male" },
            new DropdownItem { Id = 2, Name = "Female" },
            new DropdownItem { Id = 3, Name = "Non-Binary" },
            new DropdownItem { Id = 4, Name = "Not Stated" },
            new DropdownItem { Id = 5, Name = "Other" },
        };

        foreach (var item in dropdownItems)
        {
            Genders.Add(item);
        }
        pickerGuardGender.SelectedIndex = 0;
    }

    private async Task PopulateStates()
    {
        States.Clear();
        States.Add(new DropdownItem { Id = -1, Name = "Select" });

        var states = await _guardApiServices.GetStatesAsync();
        if (states != null && states.Count > 0)
        {
            int _statescount = 0;
            foreach (var item in states)
            {
                if (item.Text.ToLower() != "select")
                {
                    States.Add(new DropdownItem { Id = _statescount++, Name = item.Text });
                }
            }
        }
        pickerGuardState.SelectedIndex = 0;
    }


    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }

    private void OnFullNameTextChanged(object sender, TextChangedEventArgs e)
    {
        bool fullNameValid = IsValidFullName(GuardFullNameEntry.Text);

        FullNameBorder.Stroke = fullNameValid ? Colors.Transparent : Colors.Red;
        FullNameErrorLabel.IsVisible = !fullNameValid;
    }

    private void OnGuardInitialsTextChanged(object sender, TextChangedEventArgs e)
    {
        bool guardInitialsValid = IsValidInitials(GuardInitials.Text);

        GuardInitialsBorder.Stroke = guardInitialsValid ? Colors.Transparent : Colors.Red;
        GuardInitialsErrorLabel.IsVisible = !guardInitialsValid;
    }

    private void OnGuardStateSelected(object sender, EventArgs e)
    {
        if (initialloadState)
        {
            initialloadState = false;
            return;
        }

        bool guardStateValid = IsValidState(pickerGuardState.SelectedIndex);

        GuardStatesBorder.Stroke = guardStateValid ? Colors.Transparent : Colors.Red;
        GuardStateErrorLabel.IsVisible = !guardStateValid;
    }

    private void OnGuardGenderSelected(object sender, EventArgs e)
    {
        if (initialloadGender)
        {
            initialloadGender = false;
            return;
        }
        bool guardGenderValid = IsValidGender(pickerGuardGender.SelectedIndex);

        GuardGenderBorder.Stroke = guardGenderValid ? Colors.Transparent : Colors.Red;
        GuardGenderErrorLabel.IsVisible = !guardGenderValid;
    }

    private bool SecurityNumberIsNotValid(string SecurityNo)
    {
        if (string.IsNullOrWhiteSpace(SecurityNo))
            return true;

        // same number pattern
        // e.g: 0000, 1111, 222222 etc.
        var regex = new Regex(@"^([0-9])\1*$");

        // sequence number pattern
        // e.g: 0123, 1234, 789, 890, 123456789 etc.
        var seqPattern = "0123456789012345789";

        // sequence number pattern
        // e.g:098, 3210, 0987654321 etc.
        var revPattern = "09876543210987654321";

        return seqPattern.IndexOf(SecurityNo) != -1 || regex.IsMatch(SecurityNo) || revPattern.IndexOf(SecurityNo) != -1;
    }

    private void OnSecurityNumberTextChanged(object sender, TextChangedEventArgs e)
    {
        bool securityNumberValid = !SecurityNumberIsNotValid(GuardSecurityNumberEntry.Text);

        SecurityNumberBorder.Stroke = securityNumberValid ? Colors.Transparent : Colors.Red;
        SecurityNumberErrorLabel.IsVisible = !securityNumberValid;
    }
    
}
