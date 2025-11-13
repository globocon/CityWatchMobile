using C4iSytemsMobApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Services
{
    public static class IRPreferenceHelper
    {
        private const string Key = "LastIncidentRequest";

        public static void Save(IncidentRequest request)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    IncludeFields = true
                };

                // Optional: clear attachments to save space
                request.Attachments = null;

                string json = JsonSerializer.Serialize(request, options);
                Preferences.Set(Key, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving IR to Preferences: {ex.Message}");
            }
        }

        public static IncidentRequest? Load()
        {
            try
            {
                if (!Preferences.ContainsKey(Key))
                    return null;

                string json = Preferences.Get(Key, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    IncludeFields = true
                };

                return JsonSerializer.Deserialize<IncidentRequest>(json, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading IR from Preferences: {ex.Message}");
                return null;
            }
        }

        public static void Clear()
        {
            Preferences.Remove(Key);
        }
    }
}
