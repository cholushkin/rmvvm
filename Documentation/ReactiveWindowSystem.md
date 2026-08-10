# Reactive Window System (rWS) Architecture

## 1. Core Philosophy

The Reactive Window System (rWS) is a centralized, awaitable UI routing system designed to sit on top of the rMVVM architecture. It strictly isolates navigation intent from UI rendering.

**Key Objectives:**

* **Dumb Hierarchy:** UI roots (Containers) contain absolutely zero C# logic. They are strictly layout targets (e.g., `RectTransform` in uGUI).
* **Dynamic Routing:** Container IDs are `string` based, allowing designers to name and create unlimited canvas roots in the inspector without modifying C# enums.
* **Awaitable Flow:** Leverages `UniTask` to eliminate callback hell, allowing Modals to act as asynchronous functions that return typed payloads back to the caller.
* **DI Native:** Deeply integrated with **VContainer** to dynamically construct screens and inject dependencies seamlessly based on simple `ScriptableObject` routing requests.
* **Agnostic Transitions:** Completely decouples C# flow logic from animation libraries (like DOTween or Animator).

---

## 2. Architecture Components

### 2.1 The Dumb Containers

Containers are strictly visual hierarchy roots. They are **not** specific C# classes.

* **Implementation:** Simple `RectTransform` references mapped to a `string` ID (e.g., `"MainScreen"`, `"ModalOverlay"`) directly in the `WindowService` inspector/configuration.
* **Behavior:** They passively hold instantiated Window Composers. They do not manage state, sorting, or pooling.

### 2.2 Window Configuration (Settings)

A centralized `ScriptableObject` (`WindowConfig`) that acts as a dependency mapping tool.

* **Role:** It maps a UI Prefab to a specific `ViewModel` type and defines its default stack behavior (`Push`, `Replace`, `Clear`) and target Container ID.

### 2.3 The Window Database

A central registry (`ScriptableObject`) that stores a list of all active `WindowConfigs`.

* **Role:** It allows the `WindowService` to look up the correct visual prefab purely by querying the `ViewModel` type requested by the C# backend.

---

## 3. How We Open Windows

The `IWindowService` provides distinct ways to open a window based on context:

* **Config-Driven (`ShowAsync(WindowConfig, ...)`):** You pass a ScriptableObject asset directly. `WindowService` queries its internal map to find which ViewModel this config requires. It then asks VContainer to instantiate that ViewModel, resolving all internal dependencies automatically, spawns the prefab, and links them.
* *Use case:* Navigation bars opening screens without needing to know their internal data requirements.


* **ViewModel-Driven (`ShowAsync<TViewModel>(viewModel, ...)`):** You instantiate a ViewModel via a VContainer Factory (injecting specific runtime data) and pass it to the service. The service finds the `WindowConfig` associated with that type in the `WindowDatabase`, spawns the prefab, and binds your pre-configured ViewModel to it.
* *Use case:* Opening detail screens that require specific runtime context (e.g., clicking a specific `BuildingModel`).


* **Awaitable Modals (`ShowModalAsync<TViewModel, TResult>(...)`):** You pass a ViewModel implementing `IModalViewModel<TResult>`. The service opens the window, halts execution using `UniTask` until the user completes an action, then automatically hides the window and returns the result.
* **Generic Popups (The Extension Facade):** Pure syntactic sugar layered on top of `WindowService`. It allows developers to trigger pre-configured universal dialogs (`ConfirmDialogViewModel`, `AlertDialogViewModel`) using simple extension methods.
* *Use case:* Firing standard dialogs with one line of code: `await _windowService.ShowConfirmAsync(_quitConfig, "Quit", "Are you sure?");`



---

## 4. Hardware Back Routing

The rWS handles global hardware input (like the "Escape" key or Android "Back" button) seamlessly across the UI stack.

* **The Interface:** Modals and screens can implement `IHardwareBackHandler`.
* **The Interception:** `WindowService` monitors for back requests via `RouteHardwareBack()`. When triggered, it iterates through all active windows (prioritizing top-most overlays first) and invokes `OnBackRequested()`.
* **Consumption:** If a window handles the request (e.g., closes itself), it returns `true`, consuming the event and preventing underlying windows from closing simultaneously.

---

## 5. Animation Lifecycle

The rWS provides native support for asynchronous UI transitions without tightly coupling to any specific animation library.

* **Agnostic Interface (`IWindowTransition`):** A simple `MonoBehaviour` interface requiring two `UniTask` methods: `PlayShowAsync` and `PlayHideAsync`. This allows designers to use `DOTween`, Unity `Animator`, or simple coroutines interchangeably.
* **The `IsAnimating` State:** During transitions, `WindowComposer` flags `IsAnimating = true`.
* **Input Blocking:** Hardware Back requests and rapid user inputs are safely ignored while a window is actively animating, preventing flow breaks or null references.
* **Mixed Support:** If a window prefab has no transition component attached, the `WindowComposer` instantly toggles it active/inactive with zero execution overhead.

---

## 6. VContainer Integration (The Routing Engine)

The true power of rWS is unlocked via its integration with VContainer's `IObjectResolver`.

1. **Mapping:** During setup, `WindowDatabase` scans all assigned `WindowConfig` prefabs, reads their generic `WindowComposer<TViewModel>`, and builds a dictionary mapping the `System.Type` of the ViewModel to its corresponding `WindowConfig` asset.
2. **Resolution:** When a purely data-driven navigation request is made (for instance, calling `_windowService.ShowAsync(activitiesConfig)` using an `Activities` screen as an example), the service intercepts it:
  * It queries `WindowDatabase` to resolve `activitiesConfig` -> `ActivitiesViewModel`.
  * It calls `_resolver.Resolve(ActivitiesViewModel)`.
  * VContainer automatically inspects `ActivitiesViewModel`, identifies its required dependencies (e.g., `ActivityService`), constructs the object graph, and returns the fully hydrated ViewModel.

3. **Execution:** `WindowService` instantiates the UI Prefab and passes the resolved ViewModel to the `WindowComposer`.

This completely eliminates massive switch statements, reflection-based fallbacks (`Activator.CreateInstance`), and manually passing static services down through intermediate UI scripts.