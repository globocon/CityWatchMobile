
namespace C4iSytemsMobApp.Interface
{
    public interface IVolumeButtonService
    {
        //void Init(Action onVolumeUpPressed, Action onVolumeDownPressed);

        event EventHandler VolumeUpPressed;
        event EventHandler VolumeDownPressed;
    }
}
