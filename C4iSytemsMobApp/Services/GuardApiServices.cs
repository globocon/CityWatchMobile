using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;


namespace C4iSytemsMobApp.Services
{
    public class GuardApiServices : IGuardApiServices
    {
        int guardId;
        int clientSiteId;
        int userId;
        string guardLicenceNo;
        bool isError;
        string msg;
        public GuardApiServices()
        {
            // Constructor logic if needed
            GetSecureStorageValues();
        }

        private void GetSecureStorageValues()
        {
            string msg = string.Empty;
            int.TryParse(Preferences.Get("GuardId", "0"), out guardId);
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out clientSiteId);
            int.TryParse(Preferences.Get("UserId", "0"), out userId);
            guardLicenceNo = Preferences.Get("LicenseNumber", "");

            if (guardId <= 0)
            {
                msg = "Guard ID not found. Please validate the License Number first.";
                isError = true;
            }
            if (clientSiteId <= 0)
            {
                msg = "Please select a valid Client Site.";
                isError = true;
            }
            if (userId <= 0)
            {
                msg = "User ID is invalid. Please log in again.";
                isError = true;
            }
        }

        public async Task<List<SelectListItem>> GetStatesAsync()
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStates";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<List<SelectListItem>>();
                return rows ?? new List<SelectListItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching States: {ex.Message}");
                return new List<SelectListItem>();
            }
        }
        public async Task<(bool isSuccess, string errorMessage, NewGuard? _newGuard)> RegisterNewGuardAsync(NewGuard request)
        {
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/RegisterNewGuardFromMobile";
            HttpClient _httpClient = new HttpClient();
            var d = JsonSerializer.Serialize(request);
            var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<NewGuard>>();
                return (result.isSuccess, result.message, result.data);
            }
            else
            {
                var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse<NewGuard>>();
                return (errorResult.isSuccess, errorResult.message, null);
            }
        }


        public async Task<List<GuardComplianceAndLicense>?> GetHrRecordsOfGuard()
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardHrRecords?guardId={guardId}";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<ApiResponse<List<GuardComplianceAndLicense>>>();
                return settings.data;
            }

            return new List<GuardComplianceAndLicense>();
        }

        public async Task<(bool isSuccess, string errorMessage)> ValidateGuardDocumentAccessPin(string pin)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/ValidateGuardPinForHrRecordAccess?guardId={guardId}&key={pin}";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    return (settings.data, settings.message);
                }

                return (false, "An error occurd while validating the PIN.");
            }
            catch (Exception ex)
            {

                return (false, $"Exception occurd while validating the PIN. {ex.Message}.");
            }

        }

        public async Task<List<HRGroups>> GetHrGroups()
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetHrGroupsList";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<ApiResponse<List<HRGroups>>>();
                return rows?.data ?? new List<HRGroups>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Hr Groups: {ex.Message}");
                return new List<HRGroups>();
            }
        }

        public async Task<List<CombinedData>> GetHrGroupDescriptions(int hrGroupId)
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetHrGroupDescriptionsList?HRid={hrGroupId}&GuardID={guardId}";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<ApiResponse<List<CombinedData>>>();
                return rows?.data ?? new List<CombinedData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Hr Group Descriptions: {ex.Message}");
                return new List<CombinedData>();
            }
        }

        public async Task<bool> CheckForHrBan(int DescriptionID)
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/CheckForHrDescriptionBan?DescriptionID={DescriptionID}";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                return rows.data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for HR Ban: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool,string)> SaveHrDocument(GuardComplianceAndLicense guardComplianceAndLicense, FileResult? file)
        {
            string msg = "";

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveHrRecordOfGuard";

            using var _httpClient = new HttpClient();
            using var form = new MultipartFormDataContent();



            // ----- Add file -----
            if (file != null)
            {
                var stream = await file.OpenReadAsync();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                form.Add(fileContent, "Docfile", file.FileName);
            }

            guardComplianceAndLicense.GuardId = guardId;
            guardComplianceAndLicense.IsLogin = "false";

            // ----- Add model fields -----
            form.Add(new StringContent(guardComplianceAndLicense.Id.ToString()), "Id");
            form.Add(new StringContent(guardComplianceAndLicense.GuardId.ToString()), "GuardId");
            form.Add(new StringContent(guardComplianceAndLicense.Description ?? ""), "Description");
            form.Add(new StringContent(guardComplianceAndLicense.ExpiryDate?.ToString("yyyy-MM-dd") ?? ""), "ExpiryDate");
            form.Add(new StringContent(((int)guardComplianceAndLicense.HrGroup.Value).ToString() ?? ""), "HrGroup");
            form.Add(new StringContent(guardComplianceAndLicense.HrGroupText ?? ""), "HrGroupText");
            form.Add(new StringContent(guardComplianceAndLicense.CurrentDateTime ?? ""), "CurrentDateTime");
            form.Add(new StringContent(guardComplianceAndLicense.LicenseNo ?? ""), "LicenseNo");
            form.Add(new StringContent(guardComplianceAndLicense.Reminder1.ToString()), "Reminder1");
            form.Add(new StringContent(guardComplianceAndLicense.Reminder2.ToString()), "Reminder2");
            form.Add(new StringContent(guardComplianceAndLicense.DateType.ToString()), "DateType");
            form.Add(new StringContent(guardComplianceAndLicense.IsDateFilterEnabledHidden.ToString()), "IsDateFilterEnabledHidden");
            form.Add(new StringContent(guardComplianceAndLicense.IsLogin ?? ""), "IsLogin");
            form.Add(new StringContent(guardComplianceAndLicense.FileName ?? ""), "FileName");
            //form.Add(new StringContent(guardComplianceAndLicense.StatusColor ?? ""), "StatusColor");

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, form);
                if (response.IsSuccessStatusCode)
                {
                    var rows = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    if(rows != null)
                    {
                        if(rows.isSuccess)
                            msg = "HR Record document has been saved.";
                        else
                            msg = rows.message;
                        return (rows.isSuccess, msg);
                    }
                    else
                    {
                        msg = "Error saving HR Document";
                        return (false, msg);
                    }
                }
                else
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    Console.WriteLine($"Error saving HR Document: {errorResult.message}");
                    return (false, errorResult.message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving HR Document: {ex.Message}");
                msg = "Unable to save record.An error occured while saving the record.";
                return (false, msg);
            }
            // ----- Send request -----


        }

        public async Task<(bool IsSuccess, string msg)> DeleteHrDocument(int id)
        {

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/DeleteHrRecordOfGuard";

            using var _httpClient = new HttpClient();
            var response = await _httpClient.PostAsJsonAsync(apiUrl, id);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return (result.IsSuccess,result.message);
            }
            else
            {
                var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return (errorResult.IsSuccess, errorResult.message);
            }
        }
    }

}
