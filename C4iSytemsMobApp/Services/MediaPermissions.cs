namespace C4iSytemsMobApp.Services
{
    /// <summary>
    /// Read access to device images: READ_MEDIA_IMAGES on Android 13+ (API 33),
    /// READ_EXTERNAL_STORAGE on older versions.
    /// </summary>
    public class ReadMediaImagesPermission : Permissions.BasePlatformPermission
    {
#if ANDROID
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(33)
                ? new[] { (global::Android.Manifest.Permission.ReadMediaImages, true) }
                : new[] { (global::Android.Manifest.Permission.ReadExternalStorage, true) };
#endif
    }

#if ANDROID
    /// <summary>
    /// Android 14 (API 34) "Select photos" partial access. When the user grants partial access,
    /// READ_MEDIA_IMAGES reports denied but this permission is granted and MediaStore returns
    /// the user-selected subset.
    /// </summary>
    public class ReadMediaVisualUserSelectedPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { ("android.permission.READ_MEDIA_VISUAL_USER_SELECTED", true) };
    }
#endif
}
