

namespace C4iSytemsMobApp.Services
{
    public class BluetoothScanPermission : Permissions.BasePlatformPermission
    {
#if ANDROID
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new[]
        {
            (Android.Manifest.Permission.BluetoothScan, true),
            (Android.Manifest.Permission.BluetoothConnect, true),
            (Android.Manifest.Permission.AccessFineLocation, true)
        };
#endif
    }
}
