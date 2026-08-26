# Reactive Window System (rWS)

**Scope:** UI navigation routing. Built on top of [rMVVM.md](rMVVM.md) architecture. See [DataFlow.md](DataFlow.md) for how domain state is initialized and persisted.

## Contents

1. [Core Philosophy](#1-core-philosophy)
2. [Architecture Components](#2-architecture-components)
3. [Opening Windows](#3-opening-windows)
4. [Stack Modes](#4-stack-modes)
5. [Hardware Back Routing](#5-hardware-back-routing)
6. [Animation Lifecycle](#6-animation-lifecycle)
7. [VContainer Integration](#7-vcontainer-integration)
8. [The Reactive Window Stack](#8-the-reactive-window-stack)
9. [Consumer Case Study: Input Arbitration (InputConfig)](#9-consumer-case-study-input-arbitration-inputconfig)
10. [Known Limitations](#10-known-limitations)

---

## 1. Core Philosophy

rWS is a centralized, awaitable UI routing system layered on top of rMVVM. It strictly isolates navigation intent from UI rendering.

**Key Objectives:**

* **Dumb Hierarchy:** UI roots (Containers) are purely layout targets (`RectTransform`). Zero C# logic.
* **Dynamic Routing:** Container IDs are strings, allowing designers to create unlimited roots in the inspector without C# enum changes.
* **Awaitable Flow:** Modals act as async functions that return typed payloads, eliminating callback hell.
* **VContainer Integration:** Automatically constructs and injects dependencies into ViewModels. See [rMVVM.md: Dependency Injection](rMVVM.md#4-dependency-injection-vcontainer-boundaries).
* **Animation-Agnostic:** Decouples flow logic from animation libraries (DOTween, Animator, or plain coroutines).
* **Observable Stack:** The window stack is a first-class reactive source (§8), so cross-cutting systems (input gating, analytics, audio) react to what's open without per-screen opt-in.

---

## 2. Architecture Components

### 2.1 The Dumb Containers

Containers are strictly visual hierarchy roots. They are **not** specific C# classes.

* **Implementation:** Simple `RectTransform` references mapped to a `string` ID (e.g., `"MainScreen"`, `"ModalOverlay"`, `"Background"`) directly in the `WindowService` inspector (`ContainerMapping` list).
* **Behavior:** They passively hold instantiated Window Composers. They do not manage state, sorting, or pooling. `WindowService` keeps one `List<WindowComposer>` per container internally and treats the *last* entry as the topmost window.
* **Naming matters beyond the UI:** container IDs are also the join key other systems key off of — e.g. hardware-back priority (§5) and input arbitration (§9) both reference container IDs by string. Renaming a container in the inspector without updating those other places is a common source of silent breakage; there is no compile-time link between them.

### 2.2 Window Configuration (`WindowConfig`)

A centralized `ScriptableObject` that acts as a routing descriptor.

* **Role:** Maps a UI Prefab to a specific `ViewModel` type (via the prefab's `WindowComposer<TViewModel>`) and defines its default stack behavior (`DefaultStackMode`) and default target container (`DefaultContainerId`). Both defaults can be overridden per-call (see §3).
* **`ViewModelType`** is not a serialized field — it's computed on demand by reflecting on `Prefab`'s attached `WindowComposer`. If `Prefab` is unassigned or lacks a `WindowComposer`, it returns `null` and callers that depend on it (e.g. startup windows) log an error instead of resolving a ViewModel.

### 2.3 The Window Database (`WindowDatabase`) — an optional caller-side helper

`WindowDatabase` is a `ScriptableObject` registry that reverse-maps a ViewModel `Type` to its `WindowConfig`. **`WindowService` never touches it.** It exists purely so that a caller which only knows a ViewModel type (not a `WindowConfig` asset reference) can resolve one before calling the service itself:

```csharp
// Example from DialogService — the real production usage:
var config = _windowDatabase.GetConfig<AlertDialogViewModel>();
return _windowService.ShowAlertAsync(config, title, message, okText, ct);
```

* **Building the cache:** on first `GetConfig<T>()` call, it scans every `WindowConfig` in its `Configs` list, inspects each `Prefab`'s `WindowComposer<TViewModel>` generic argument via reflection, and builds a `Dictionary<Type, WindowConfig>`. This only happens once per `WindowDatabase` instance (lazy, cached).
* **When to use it:** for call sites that are generic over "any dialog-shaped ViewModel" and don't want a `[SerializeField] WindowConfig` for every screen they might open (`DialogService`, `BaseScreenViewModel`'s confirm/alert flows). For a screen with one fixed, known `WindowConfig`, just hold the reference directly and skip `WindowDatabase` — it's overhead for no benefit in that case.

---

## 3. Opening Windows

`IWindowService` provides several ways to open a window, from fully data-driven to fully explicit:

* **Fluent builder (`Open(config)` / `Open<TViewModel>(config)`):** returns a `WindowBuilder` struct with `.InContainer(id)`, `.WithMode(mode)`, and (generic overload only) `.WithViewModel(vm)`, terminated by `.ExecuteAsync(ct)`. This is the most explicit form; the other `ShowAsync` overloads are sugar for it internally.
  ```csharp
  // [Project Example] "Activities" is a game-layer screen folder
  await _windowService.Open<ActivitiesViewModel>(_activitiesConfig)
      .WithViewModel(viewModel)
      .WithMode(StackMode.Push)
      .ExecuteAsync();
  ```
* **Config-Driven (`ShowAsync<TViewModel>(config, ct)`):** resolves `TViewModel` via VContainer (`_resolver.Resolve<TViewModel>()`) and shows it. *Use case:* screens with no runtime context.
* **ViewModel-Driven (`ShowAsync<TViewModel>(config, viewModel, mode, ct)`):** pass a pre-constructed ViewModel (typically from a VContainer factory). *Use case:* detail screens keyed on a specific domain instance (e.g., opening a building inspector for a specific building). See [rMVVM.md](rMVVM.md#c---c--vcontainers-job) for factory patterns.
* **Awaitable Modals (`ShowModalAsync<TViewModel, TResult>(...)`):** `TViewModel` must implement `IModalViewModel<TResult>` (a single `UniTask<TResult> ResultTask` property). The service shows the window, awaits `ResultTask`, then automatically calls `HideAsync` and returns the result to the caller. Two overloads exist: pass a pre-built modal ViewModel, or let the service resolve one and pass a `setup` callback to configure it in place.
* **Generic Popups (`WindowServiceExtensions`):** pure extension-method sugar over `ShowModalAsync` for the two universal dialog ViewModels (`ConfirmDialogViewModel`, `AlertDialogViewModel`):
  ```csharp
  bool confirmed = await _windowService.ShowConfirmAsync(_dialogConfig, "Quit", "Are you sure?");
  await _windowService.ShowErrorAsync(_dialogConfig, "Something went wrong.");
  ```

---

## 4. Stack Modes

`StackMode` (on `WindowConfig.DefaultStackMode`, overridable per-call) controls what happens to the *target container's* existing content before the new window is shown:

| Mode | Current behavior |
|---|---|
| `Push` | Adds the new window on top; nothing existing is touched. |
| `Replace` | Hides/destroys **only the topmost** window in the container first, then shows the new one. |
| `Clear` | ⚠️ Currently identical to `Replace` — see [§10](#10-known-limitations). Only the topmost window is removed, *not* the whole container. |

Both `Replace` and `Clear` route through the same internal call, `HideAsync(containerId)`, which only ever pops the last entry of that container's list. If you need true "clear everything in this container" semantics today, call `HideAsync(containerId)` in a loop from the caller until `GetActiveWindowCount(containerId) == 0` (see §8) — don't rely on `StackMode.Clear` to do it for you yet.

---

## 5. Hardware Back Routing

The rWS handles global hardware input (the "Escape" key, Android "Back" button) across the UI stack via `RouteHardwareBack()`.

* **The Interface:** Modals and screens opt in by having their ViewModel implement `IHardwareBackHandler` (`bool OnBackRequested()`).
* **The Interception:** `RouteHardwareBack()` walks containers in a **hardcoded priority order** — `"ModalOverlay"`, then `"MainScreen"`, then `"Background"` — and within each container, top-to-bottom. This order is not configurable from the inspector; see [§10](#10-known-limitations).
* **Animating windows short-circuit the whole call:** if the topmost window in the *first non-empty* container in that priority order `IsAnimating`, `RouteHardwareBack()` returns `true` immediately (input "consumed") without even checking that window's `IHardwareBackHandler` — this is intentional, to prevent back-spamming from interrupting a transition.
* **Consumption:** otherwise, if the topmost window implements `IHardwareBackHandler` and its `OnBackRequested()` returns `true`, the event is consumed and lower-priority containers/windows are never asked.

---

## 6. Animation Lifecycle

rWS supports asynchronous UI transitions without coupling to any specific animation library.

* **Agnostic Interface (`IWindowTransition`):** a `MonoBehaviour` interface with two `UniTask` methods, `PlayShowAsync` / `PlayHideAsync`. Attach an implementation using DOTween, `Animator`, or plain coroutines — `WindowComposer` only calls the interface.
* **The `IsAnimating` flag:** set on `WindowComposer` for the duration of the transition. While `true`, the composer also forces its `CanvasGroup.interactable` to `false` (restoring the original value afterward) so users can't double-click through a transition, and `RouteHardwareBack()` treats it as "consumed" (§5).
* **No-transition fallback:** if a window prefab has no `IWindowTransition` component, `ShowAsync`/`HideAsync` just toggle `gameObject.SetActive` instantly — zero overhead, no animation.

---

## 7. VContainer Integration

rWS resolves ViewModels through VContainer's `IObjectResolver` — it does **not** consult `WindowDatabase` (that's a caller-side helper; §2.3). Two paths depending on which entry point:

1. **Type-parameterized** (`ShowAsync<TViewModel>(config)`) — `_resolver.Resolve<TViewModel>()` automatically inspects the constructor and injects all dependencies. See [rMVVM.md: DI Boundaries](rMVVM.md#c---c--vcontainers-job) for details.
2. **Non-generic** (`Open(config)`) — no compile-time type, so rWS reflects `config.Prefab`'s `WindowComposer<T>` to extract the ViewModel `Type`, then calls the non-generic `_resolver.Resolve(Type)` overload.

In both cases, the ViewModel is bound to the View via `composer.BindBoxed(viewModel)`, invoking the Composer's strongly-typed `Bind(TViewModel)` method. See [rMVVM.md: The Composer](rMVVM.md#33-composer-binder--lifecycle-owner) for the binding lifecycle.

---

## 8. The Reactive Window Stack

Beyond opening/closing windows, `IWindowService` exposes the stack itself as an observable source, so other systems can *derive* state from "what's currently open" instead of every window having to imperatively announce itself:

```csharp
Observable<WindowStackChange> ObserveStackChanged();
int GetActiveWindowCount(string containerId);
IReadOnlyList<WindowComposer> GetActiveWindows(string containerId);
```

* **`WindowStackChange`** is a small readonly struct — `ContainerId`, `Window`, `ContainerCount` (post-mutation), `Kind` (`Added`/`Removed`) — pushed through an `R3.Subject` from the three places `WindowService` actually mutates a container's list (`ExecuteShowAsync`, and both `HideAsync` overloads). It's a struct and the notify call sites are allocation-free.
* **`GetActiveWindows(containerId)`** returns the live internal `List<WindowComposer>` as an `IReadOnlyList` — no copy. Treat it as a snapshot valid until the next stack mutation; don't hold onto it across an `await`.
* **Why derive instead of push/pop:** a naive design would have every modal-opening composer call something like `inputService.PushBlock()` in `Bind()` and `PopBlock()` in `OnDestroy()`. That's a stack that must balance perfectly — miss one `Pop` (early return, exception, a composer destroyed by something other than `HideAsync`) and the block leaks forever with no way to recover except a restart. Deriving state from `ObserveStackChanged()` + `GetActiveWindows()` instead means the consumer recomputes its own state fresh on every change; a bug produces a *wrong* answer for one frame, not a *permanently stuck* one.
* **The pattern for a new cross-cutting consumer:** subscribe to `ObserveStackChanged()`, and on each notification, call `GetActiveWindows(containerId)` for whichever container(s) you care about and inspect `composer.GetViewModelBoxed()`. This is how you read window-specific policy without rWS needing to know your interface — cast it:
  ```csharp
  if (windows[i].GetViewModelBoxed() is ISceneInputPolicy policy && !policy.BlocksSceneInput)
      continue; // this window opted out
  ```
  rWS defines none of the `is ISomeInterface` contracts consumers use here — that keeps rWS itself free of Game-layer concerns. §9 walks through the concrete example this pattern was built for.

---

## 9. Consumer Case Study: Input Arbitration (`InputConfig`)

> This section documents a **Game-layer consumer** of rWS (`Game.Input.InputService`, under `Assets/Game/Runtime/Game/Input/`), not part of rWS itself. It's included here because it's the reference implementation of the §8 pattern, and because `InputConfig` maintenance is easy to get subtly wrong.

### The problem it solves

Three input layers can conflict on this project: debug overlays, UI, and the 3D scene camera. `Game.Input.InputService` centrally enables/disables three `InputActionMap`s (`Debug`, `UI`, `Scene`) on a shared `InputActionAsset`, derived from two independent, always-recomputed signals:

1. **Window-stack occupancy** (via §8) — is a "blocking" container currently occupied?
2. **An orthogonal debug-exclusive gate** — is a debug overlay flagged as exclusive currently open? (Tracked as a `HashSet`, not a stack, so two exclusive overlays open at once don't prematurely re-enable input when only one of them closes.)

Debug-exclusive, when true, disables **both** UI and Scene; window-stack occupancy only disables Scene (open windows are themselves UI, so they never block UI input).

### `InputConfig` — the ScriptableObject you maintain

Create via `Create > Game > Input > Input Config`, assigned to `GameLifetimeScope._inputConfig`. Two fields:

* **`BlockingContainers : List<string>`** — container IDs whose non-zero occupancy disables the `Scene` map by default. Ships with `["ModalOverlay"]`.
  * **Maintenance rule:** this list is a set of plain strings, matched against the same container-ID strings configured on the scene's `WindowService` (§2.1) and used as `WindowConfig.DefaultContainerId`. There is **no compile-time link** between them — renaming a container in `WindowService`'s inspector, or introducing a *new* modal-style container, does not automatically update this list.
  * **When you add a new container that should block scene input** (e.g. a second modal layer, a full-screen loading overlay container): add its ID string here. Until you do, windows opened into it will **not** block the camera/scene input, even though they visually cover it.
  * **When you add a window into an *existing* blocking container** (typically `ModalOverlay`): nothing to do — it's covered automatically by container occupancy.
  * **A single window that should not block scene input despite living in a blocking container** (e.g. a non-modal toast placed in `ModalOverlay` for z-ordering reasons only): don't touch `InputConfig` — implement `Game.Input.ISceneInputPolicy` on that window's ViewModel instead (`BlocksSceneInput => false`). `InputConfig.BlockingContainers` is a container-level default; `ISceneInputPolicy` is the per-window override.
* **`ExclusiveOverlays : List<DebugOverlayBase>`** — specific debug overlay **instances** (from `Assets/Libs/Gamelib/Runtime/Debug/Overlays/`) that take exclusive control of input (disable UI + Scene) while shown. Overlays not in this list open/close freely with no effect on other input layers — this is the "optional per-overlay" behavior by construction, no extra flag needed.
  * **Maintenance rule:** this is a list of scene/prefab *instances*, not types. If a debug overlay GameObject is deleted and re-created (rather than edited in place), or the debug tooling is moved into a different prefab, the stale reference must be replaced — Unity will silently drop a missing reference rather than error, so a "used to be exclusive, now isn't" bug from a rebuilt overlay is easy to miss. Spot-check this list after any restructuring of the debug overlay hierarchy.
  * Adding a *new* debug overlay does **not** require touching this list unless you specifically want it to be exclusive.

### Quick checklist when adding a new modal-style window

1. Does it belong in an existing blocking container (`ModalOverlay`)? → nothing to do in `InputConfig`.
2. Is it a *new* container that should also block Scene input? → add its ID to `BlockingContainers`.
3. Should this *specific* window, despite being in a blocking container, leave Scene input alone? → implement `ISceneInputPolicy` on its ViewModel, don't touch `InputConfig`.
4. Is it a debug overlay that should take over all input while open? → add its instance to `ExclusiveOverlays`.

---

## 10. Known Limitations

Carried over from `// todo:`/`// idea:` markers in the source, consolidated here for visibility:

* **`RouteHardwareBack()`'s container priority is hardcoded** (`"ModalOverlay"`, `"MainScreen"`, `"Background"`) rather than configurable — see the `todo:` at the top of `WindowService.cs`. A new container that should participate in back-routing priority requires a code change, not just an inspector edit.
* **`StackMode.Clear` doesn't actually clear the container** — see §4. It currently behaves identically to `Replace` (removes only the topmost window). Fixing this means changing `HideAsync(containerId)`'s single-pop behavior to a loop for the `Clear` case specifically, without changing `Replace`'s single-pop semantics.
* **`WindowConfig.Prefab` is a direct reference, not Addressables-based** — every configured prefab is a hard reference pulled into memory, per the `todo:` in `WindowConfig.cs`.
* **No per-window `OnWindowShown`/`OnWindowHidden` events** on `WindowComposer` — superseded by `IWindowService.ObserveStackChanged()` (§8), which reports the same information at the service level instead of requiring every composer to wire its own event.
* **Dialog extension methods take raw strings only** — no localization-key overloads yet (`todo:` in `WindowServiceExtensions.cs`).

---

## 11. See Also

* [rMVVM.md](rMVVM.md) — MVVM architecture (Model, ViewModel, Composer, View), DI boundaries, and UI assembly patterns.
* [DataFlow.md](DataFlow.md) — How domain state is initialized and persisted between sessions.
* [README.md](../README.md) — Quick feature overview.
