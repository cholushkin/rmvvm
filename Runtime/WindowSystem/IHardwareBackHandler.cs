// todo: Support asynchronous back handling (UniTask<bool>) if confirmation popups are needed in the future
// idea: Route gamepad "B" button / Cancel action natively into this handler as well

namespace Game.UI.WindowSystem
{
    public interface IHardwareBackHandler
    {
        bool OnBackRequested();
    }
}