# Reactive Window System (rWS) Architecture

## 1. Core Philosophy
The Reactive Window System (rWS) is a centralized, awaitable UI routing system designed to sit on top of the rMVVM architecture. It strictly isolates navigation intent from UI rendering.

**Key Objectives:**
* **Dumb Hierarchy:** UI roots (Containers) contain absolutely zero C# logic. They are strictly layout targets (e.g., `RectTransform` in uGUI).
* **Dynamic Routing:** Container IDs are `string` based, allowing designers to name and create unlimited canvas roots in the inspector without modifying C# enums.
* **Awaitable Flow:** Leverages `UniTask` to eliminate callback hell, allowing Modals to act as asynchronous functions that return typed payloads back to the caller.
* **DI Native:** Deeply integrated with **VContainer** to dynamically construct screens and inject dependencies seamlessly based on simple `ScriptableObject` routing requests.

---

## 2. Architecture Components

### 2.1 The Dumb Containers
Containers are strictly visual hierarchy roots. They are **not** specific C# classes.
* **Implementation:** Simple `RectTransform` references mapped to a `string` ID (e.g., `"MainScreen"`, `"ModalOverlay"`) directly in the `WindowService` inspector/configuration.
* **Behavior:** They passively hold instantiated Window Composers. They do not manage state, sorting, or pooling.

### 2.2 Window Configuration (Settings)
A centralized `ScriptableObject` (`WindowConfig`) that acts as a dependency mapping tool.
* **Role:** It maps a UI Prefab (the `WindowComposer`) to the system. Routing instructions (where to put it, how to stack it) are intentionally excluded from the config to ensure the prefab can be reused across different contexts.

### 2.3 The Composer (The Adapter & Lifecycle Owner)
Every `WindowConfig` prefab must have a `WindowComposer<TViewModel>` at its root.
* **Role:** It acts as the bridge. When `WindowService` spawns the prefab, it passes the VContainer-injected ViewModel into the Composer via `BindBoxed()`.
* **Ownership:** The Composer introduces the ViewModel to the local `View` and manages the `destroyCancellationToken`. When the window is closed and destroyed, the Composer ensures all local `R3` subscriptions and the ViewModel are properly disposed.

---

## 3. How We Open Windows

The `IWindowService` provides distinct ways to open a window:

* **Config-Driven (`ShowAsync(WindowConfig, ...)`):**
  You pass a ScriptableObject asset directly. `WindowService` queries its internal map to find which `ViewModel` this config requires. It then asks **VContainer** (`IObjectResolver`) to instantiate that ViewModel, resolving all internal dependencies automatically, spawns the prefab, and links them.
  *Use case:* Navigation bars opening screens without needing to know their internal data requirements.

* **ViewModel-Driven (`ShowAsync<TViewModel>(viewModel, ...)`):**
  You instantiate a `ViewModel` via a VContainer Factory (injecting specific runtime data) and pass it to the service. The service finds the `WindowConfig` associated with that type, spawns the prefab, and binds your pre-configured ViewModel to it.
  *Use case:* Opening detail screens that require specific runtime context (e.g., clicking a specific `BuildingModel`).

* **Awaitable Modals (`ShowModalAsync<TViewModel, TResult>(...)`):**
  You pass a ViewModel implementing `IModalViewModel<TResult>`. The service opens the window, halts execution using `UniTask` until the user completes an action, then automatically hides the window and returns the result.

---

## 4. VContainer Integration (The Routing Engine)

The true power of rWS is unlocked via its integration with VContainer's `IObjectResolver`.

1. **Mapping:** During setup, `WindowService` scans all `WindowConfig` prefabs, reads their generic `WindowComposer<TViewModel>`, and builds a dictionary mapping the `WindowConfig` asset to the `System.Type` of its ViewModel.
2. **Resolution:** When a purely data-driven request is made (e.g., `_windowService.ShowAsync(activitiesConfig)`), the service intercepts it:
    * It looks up `activitiesConfig` -> `ActivitiesViewModel`.
    * It calls `_resolver.Resolve(ActivitiesViewModel)`.
    * VContainer automatically detects that `ActivitiesViewModel` needs `ActivityService`, builds it, and returns the fully hydrated ViewModel.
3. **Execution:** `WindowService` instantiates the Prefab and passes the resolved ViewModel to the Composer.

This completely eliminates massive switch statements, reflection-based fallbacks (`Activator.CreateInstance`), and manually passing static services down through intermediate UI scripts.