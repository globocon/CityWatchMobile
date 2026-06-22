

namespace C4iSytemsMobApp.Services
{
    public static class PermissionService
    {
        public static async Task<bool> CheckAndRequestPermissionsAsync()
        {
#if ANDROID
            var statusLocation = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (statusLocation != PermissionStatus.Granted)
            {
                statusLocation = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (statusLocation != PermissionStatus.Granted)
                    return false;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                //var statusScan = await Permissions.CheckStatusAsync<Permissions.BluetoothScan>();
                //if (statusScan != PermissionStatus.Granted)
                //    statusScan = await Permissions.RequestAsync<Permissions.BluetoothScan>();

                //var statusConnect = await Permissions.CheckStatusAsync<Permissions.BluetoothConnect>();
                //if (statusConnect != PermissionStatus.Granted)
                //    statusConnect = await Permissions.RequestAsync<Permissions.BluetoothConnect>();

                //if (statusScan != PermissionStatus.Granted || statusConnect != PermissionStatus.Granted)
                //    return false;

                var status = await Permissions.CheckStatusAsync<BluetoothScanPermission>();

                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<BluetoothScanPermission>();

                return status == PermissionStatus.Granted;
            }
#endif
            return true;
        }


        public static async Task<string> CheckAndGetGpsLocationAsync()
        {
            var _LocationString = "";
            var _CheckPermission = await CheckIfHasLocationPermission();
            if (_CheckPermission)
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                if (location != null)
                {
                    Preferences.Set("GpsCoordinates", location.Latitude.ToString() + ',' + location.Longitude.ToString());
                    _LocationString = $"{location.Latitude.ToString()},{location.Longitude.ToString()}";
                }                
                return _LocationString;
            }

            return _LocationString;
        }


        public static async Task<string> GetGpsLocationWithOutCheckingPermissionAsync()
        {
            var _LocationString = Preferences.Get("GpsCoordinates", "");
            var _CheckPermission = await CheckLocationPermission();
            if (_CheckPermission)
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                if (location != null)
                {
                    Preferences.Set("GpsCoordinates", location.Latitude.ToString() + ',' + location.Longitude.ToString());
                    _LocationString = $"{location.Latitude.ToString()},{location.Longitude.ToString()}";
                }
                return _LocationString;
            }

            return _LocationString;
        }

        public static async Task<bool> CheckIfHasLocationPermission()
        {
            var statusLocation = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (statusLocation != PermissionStatus.Granted)
            {
                statusLocation = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (statusLocation != PermissionStatus.Granted)
                    return false;
                else return true;
            }
            else
            {
                return true;
            }
        }

        public static async Task<bool> CheckLocationPermission()
        {
            var statusLocation = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (statusLocation != PermissionStatus.Granted)
            {
              return false;
            }
            else
            {
                return true;
            }
        }

    }
}
