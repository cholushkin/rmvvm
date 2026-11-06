# Reactive Window System (rWS) — Reusable Navigation Contract

**Status:** Target contract for the reusable UI/window-system module  
**Related:** [rMVVM.md](rMVVM.md) · [DataFlow.md](DataFlow.md)

> **Module boundary:** rWS knows only navigation, windows, modal results, containers, binding entry points, and window lifecycles. It does **not** reference a consuming game's domain models, screen-specific types, input action maps, clocks, currencies, rules, or services. Game projects import and configure this module.

## 1. Principles

- Centralize window creation, stack changes, animated show/hide, navigation, back handling, and modal results.
- Keep rendering and game behavior outside routing: a container is a visual parent, and a `WindowConfig` is a routing descriptor.
- Use VContainer for ViewModel construction and `WindowComposer<TViewModel>` for window-root binding. No Composer is required for a simple nested widget.
- Preserve explicit, awaitable APIs and allow caller-supplied ViewModels when runtime context is required.
- Make stack state observable so host systems can *derive* their own behavior without rWS depending on those systems.
- Route lifecycle and cancellations deterministically. An open/close transition must not strand a task or subscription.

## 2. Core pieces

### 2.1 Container mappings

`WindowService` maps configurable **string** container IDs to Unity `RectTransform` roots. Each container maintains an ordered list/stack of windows; its last entry is topmost.

- Container IDs are configuration keys, not game concepts.
- Reject duplicate/blank container IDs; fail visibly for unknown IDs and missing roots.
- Validate references from `WindowConfig` and back-routing priorities in the editor or on startup.
- Avoid assuming hardcoded IDs such as `"ModalOverlay"`, `"MainScreen"`, or `"Background"`. These may be examples of host configuration, not library requirements.
- Optional developer-hygiene check: `_killObjectsUnderRootOnAwake` (Inspector flag, off by default). When enabled, `WindowService.Awake` warns about and destroys any enabled object found directly under a container root — a leftover test/debug object a developer forgot to remove before committing the scene. Only direct children of each root are checked; windows are always added as direct children, so anything deeper is inside a window's own prefab hierarchy, not a stray.

### 2.2 WindowConfig

A `ScriptableObject` descriptor with:

- Prefab containing a `WindowComposer<TViewModel>` root.
- Default target container ID.
- Default stack mode.
- Optional transition/presentation settings where useful.

The ViewModel type may be resolved from the prefab's `WindowComposer<T>` generic argument; it need not be redundantly serialized. Invalid/missing composer must give a clear validation error.

### 2.3 WindowDatabase (optional)

A caller-side catalog maps ViewModel types to configs for generic dialogs or type-driven callers. It is **not** required for every screen; `WindowService` should accept a direct `WindowConfig` and should not depend on a `WindowDatabase` existing. Cache the reverse lookup and report duplicate ViewModel registrations instead of silently choosing one.

## 3. Opening windows and ViewModels

Keep existing public entry-point concepts where possible:

```csharp
// Illustrative use; actual names/overloads follow the installed rWS API.
await windowService.Open<DetailsViewModel>(detailsConfig)
    .WithViewModel(detailsViewModel)
    .WithMode(StackMode.Push)
    .ExecuteAsync(cancellationToken);
```

- `Open(config)` and `Open<TViewModel>(config)` return a fluent builder with container/mode overrides and optional explicit VM.
- `ShowAsync<TViewModel>(config, ct)` resolves a ViewModel from the **UI** VContainer scope.
- `ShowAsync<TViewModel>(config, viewModel, ..., ct)` uses a caller-provided instance.
- `ShowModalAsync<TViewModel,TResult>(...)` shows a modal implementing the typed-result contract, awaits it, closes the window, and returns the result.
- Conveniences for confirm/alert dialogs are optional extensions; do not hardcode application-specific text, localization assumptions, or content IDs into rWS.
- A ViewModel requiring a selected entity/ID should be created by a factory supplied by the consuming game, not hardcoded into the framework.

## 4. Stack modes (required semantics)

| Mode | Contract |
| --- | --- |
| `Push` | Open a new window above current windows without removing them. |
| `Replace` | Close/remove **only the topmost** window, then open the new one. |
| `Clear` | Close/remove **all existing windows in the target container**, then open the new one. |

`Clear` must **not** behave as `Replace`. If the present implementation removes only one topmost window, that behavior is a defect to correct in the reusable package. Other containers are unaffected. Close operations must handle in-flight transitions without corrupting the stack.

## 5. Window composition, lifecycle and ownership

An rWS window prefab uses `WindowComposer<TViewModel>` as a **thin mandatory root adapter**. That Composer can either:

1. bind ViewModel values to Unity UI elements itself; or
2. delegate binding to a View component, including nested widgets and observable-list rendering.

The second pattern is often simpler; rWS must not enforce a separate Composer for every row or icon.

**Required lifecycle behavior:**

- Bind once per opening; provide a clear unbind/close phase for rebind/pooling cases.
- Unsubscribe all Composer/View R3 subscriptions and Unity button handlers on unbind/destroy.
- Distinguish **window-owned** ViewModels from **caller-owned** instances. Closing a window only disposes a VM the window explicitly owns; externally supplied instances remain caller-owned unless ownership was explicitly transferred.
- Avoid double disposal when VContainer also owns the instance; document which layer is responsible for transient disposal.
- When hiding, replacing, clearing, destroying a parent scope, or cancelling, complete/cancel pending modal tasks predictably. Never leave an `await ShowModalAsync` hanging indefinitely.
- Async show/hide failures must result in a consistent window stack and released subscriptions/resources.

## 6. Transitions

`IWindowTransition` (or equivalent current API) provides awaitable `PlayShowAsync`/`PlayHideAsync`, independent of animation technology. A root may use an Animator, LitMotion, or an instant active-state fallback.

- `IsAnimating` is true during transitions; interaction is blocked while a transition is active.
- Repeated back/close input during animation is consumed or queued by an explicit rule, not allowed to create duplicate transitions.
- Maintain reliable cleanup if cancellation or destruction occurs mid-animation.
- The no-transition path should remain lightweight and deterministic.

## 7. Hardware Back routing

Keep the window-level `IHardwareBackHandler` concept. Traverse visible windows from top priority to bottom and allow a handler to consume the action.

**Target improvement:** back-routing container priorities are data-driven/configurable rather than a hardcoded list of particular IDs. The first topmost animating window may consume back to prevent transition spam, according to the documented policy. Missing handlers fall through according to stack priority; expose a clear default-close policy if desired.

The library does not own keyboard bindings or platform-specific input actions; the host forwards an abstract back request to the router.

## 8. Reactive stack inspection

Keep the existing observable stack model:

```csharp
Observable<WindowStackChange> ObserveStackChanged();
int GetActiveWindowCount(string containerId);
IReadOnlyList<WindowComposer> GetActiveWindows(string containerId);
```

`WindowStackChange` includes a container ID, added/removed window, post-change count and kind. Emit changes at **all** real stack mutation points, including exceptional cleanup and externally destroyed window roots. Prevent stale entries.

`GetActiveWindows` may expose a read-only view of a live internal list for low allocation, but callers **must not retain it across an `await`** or assume it is a frozen snapshot. A host needing a stable snapshot copies it.

Use this state to derive policies instead of hand-maintained push/pop flags. A consumer may inspect a ViewModel through an optional contract without rWS knowing the contract type in advance.

## 9. Host integration: input, pause and application policy

rWS publishes navigation/stack facts. The **consuming application** decides their effects:

- Input arbitration: a host can decide which scene input to disable based on occupied containers and optionally per-window policy interfaces.
- Pausing a simulation: a host can observe certain window types/stack states and request clock pause; rWS itself has no game clock dependency and does not pause time.
- Audio, analytics, accessibility, localization, focus changes, and debug overlays can subscribe or adapt externally.

Do not embed a specific game's `InputService`, `InputConfig`, action map names, overlay types, or default blocking-container policy into the reusable window package. A host may use the pattern, but it belongs in host documentation/code.

## 10. VContainer and package boundary

- Register window services and ViewModel factories in the **child UI scope**, when a split session/UI hierarchy exists.
- Domain/services from a parent session scope may be injected into UI ViewModels; parent domain services cannot depend on child-only UI services.
- Keep a game-specific `WindowDatabase`/configuration in the host project, even if the generic `WindowDatabase` type lives in rWS.
- Do not reference any consuming-game assemblies in the architecture/window package. Pass host-specific behavior through injected adapters or optional interfaces defined outside the core package.
- `RegisterComponent`/scene references belong in the UI scope installer, not static global lookups scattered throughout the library.

See [DataFlow.md](DataFlow.md) for scope/installer rules and [rMVVM.md](rMVVM.md) for binding ownership.

## 11. Conformance and implementation checklist

- `Push`, `Replace`, and `Clear` have distinct tested behavior.
- Unknown/duplicate container IDs and missing composers produce actionable errors.
- Back routing works with arbitrary configured container IDs (no hardcoded priority names).
- Opening/closing the same window repeatedly releases subscriptions and owned ViewModels.
- Caller-supplied ViewModels are not disposed by rWS unless explicitly transferred.
- Cancelled/externally destroyed modals do not leave unresolved result tasks.
- Stack observations stay consistent after close, clear, failed transitions, and unexpected destruction.
- No game-specific model/service/input/clock/content symbols appear in the reusable package.
- Non-window widgets do not require `WindowComposer<T>`.

## 12. Related documents

- [rMVVM.md](rMVVM.md) — reusable reactive presentation architecture.
- [DataFlow.md](DataFlow.md) — data ownership, session lifetimes and initialization.
