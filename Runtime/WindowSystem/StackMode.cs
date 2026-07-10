// - todo: consider adding 'InsertBehind' mode for background loading
// - todo: evaluate if 'ReplaceAll' is needed for complete resets
// - todo: document edge cases for Push mode overlapping
// - todo: unit test StackMode parsing if needed later
// - todo: keep enum lean to prevent routing complexity

namespace Game.UI.WindowSystem
{
    /// <summary>
    /// Defines how a window is added to the target container's hierarchy.
    /// </summary>
    public enum StackMode
    {
        /// <summary>
        /// Destroys the current topmost window in the container before showing the new one.
        /// </summary>
        Replace, 
        
        /// <summary>
        /// Adds the new window as the last sibling, rendering it on top of existing windows.
        /// </summary>
        Push,
        
        /// <summary>
        /// Destroys all active windows in the container before showing the new one.
        /// </summary>
        Clear
    }
}