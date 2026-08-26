// idea: carry the ViewModel too, so subscribers can avoid GetViewModelBoxed() round-trips

namespace Game.UI.WindowSystem
{
    public enum WindowStackChangeKind
    {
        Added,
        Removed
    }

    /// Emitted by <see cref="IWindowService.ObserveStackChanged"/> whenever a window is added to or
    /// removed from a container. Carries the post-mutation occupancy so subscribers can derive state
    /// (e.g. input gating) without re-reading the service.
    public readonly struct WindowStackChange
    {
        public readonly string ContainerId;
        public readonly WindowComposer Window;
        public readonly int ContainerCount;
        public readonly WindowStackChangeKind Kind;

        public WindowStackChange(string containerId, WindowComposer window, int containerCount, WindowStackChangeKind kind)
        {
            ContainerId = containerId;
            Window = window;
            ContainerCount = containerCount;
            Kind = kind;
        }
    }
}
