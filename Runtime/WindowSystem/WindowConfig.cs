// todo: integrate Addressables instead of direct Prefab references
// todo: add default transition override (Fade vs Slide)

using System;
using Game.UI.WindowSystem;
using UnityEngine;

[CreateAssetMenu(menuName = "GameLib/UI/Window Config", fileName = "WindowConfig")]
public class WindowConfig : ScriptableObject
{
    [Tooltip("The default layout root this window should spawn in (e.g., 'MainScreen'). Can be overridden via the WindowBuilder.")]
    public string DefaultContainerId = "MainScreen";

    [Tooltip("The default behavior for how this window affects the current window stack.")]
    public StackMode DefaultStackMode = StackMode.Replace;

    [Tooltip("The actual UI Prefab GameObject. It MUST have a script attached that inherits from WindowComposer.")]
    public GameObject Prefab;

    /// Dynamically evaluates the required ViewModel type by inspecting the assigned Prefab's Composer.
    public Type ViewModelType
    {
        get
        {
            if (Prefab != null)
            {
                var composer = Prefab.GetComponent<WindowComposer>();
                if (composer != null)
                {
                    return composer.GetViewModelType();
                }
            }
            return null;
        }
    }
}