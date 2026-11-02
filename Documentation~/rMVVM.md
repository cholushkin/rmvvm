# Reactive MVVM (rMVVM) — Reusable Architecture

**Status:** Target architecture contract for a reusable Unity module  
**Dependencies:** C#, Unity, VContainer, R3, ObservableCollections, VitalRouter, and an async abstraction such as UniTask where needed  
**Related:** [DataFlow.md](DataFlow.md) · [ReactiveWindowSystem.md](ReactiveWindowSystem.md)

> **Module boundary:** This document describes a reusable architecture package, not an individual game. It must contain no project-specific rules, data models, content IDs, screen types, or dependencies on a consuming game's assemblies. Games depend on this module, not the reverse.

## 1. Purpose and design priorities

The goal is readable, testable presentation and gameplay code for small teams without obligating each visual element to use a heavyweight MVVM stack.

1. **State is observable; behavior has explicit owners.** R3 exposes changing values, ObservableCollections handles dynamic lists, and domain/application services enforce gameplay rules.
2. **Both direct service calls and VitalRouter are valid.** Choose on coupling and semantics, not a mandatory command-bus rule.
3. **Presentation logic is independent of a specific UI technology.** ViewModels should not reference uGUI or UI Toolkit components. Ordinary Unity value types and assets (for example `Sprite` or `Vector2`) may be used where practical; absolute Unity independence is optional.
4. **Prefer fewer classes.** A ViewModel is useful where presentation logic, computed state, or reactive behavior warrants it; simple labels and icons need no standalone MVVM triad.
5. **VContainer owns dependency construction; rWS owns window routing.** A Composer is the required adapter for an rWS window root, not an obligatory extra layer for every nested widget.
6. **Explicit lifetimes beat global singletons.** A screen ViewModel normally lives for an opening, and session state belongs to the session scope. See [DataFlow.md](DataFlow.md).

## 2. Responsibilities

| Component | Owns | Does not own |
| --- | --- | --- |
| **Definition/configuration** | Static designer-authored data, validation, small configuration-local formulas | Mutable game-session state or UI subscriptions |
| **Model** | Authoritative runtime state, simple invariant-preserving methods and computed getters | UI formatting, window routing, scene object lifetimes |
| **Domain/application service** | Operations, validation, coordinated state changes, timers and side effects | UI rendering and widget layout |
| **ViewModel** | Presentation data, filtering, formatting, button enablement, forwarding user intentions | Gameplay rules, Unity UI element references |
| **Composer / root adapter** | Connecting a window ViewModel to its Unity presentation, navigation-specific wiring and lifecycle integration | Business rules, direct construction of domain services |
| **View / widget** | UI references, visual binding, prefab child creation, local interaction wiring | Authoritative gameplay state and cross-system operations |

A `View` may subscribe directly to a ViewModel and manage child widgets. This is intentional, **not** a violation of rMVVM. Split View and Composer only when the split improves clarity. For rWS windows, the small `WindowComposer<TViewModel>` root remains the registration/binding entry point; it may delegate `Bind` to a View. For non-window widgets, no Composer is required.

## 3. R3 state and event semantics

### 3.1 `ReactiveProperty<T>` versus `ReadOnlyReactiveProperty<T>` versus `Observable<T>`

- `ReactiveProperty<T>` is a mutable observable value. The owner can set `.Value`.
- `ReadOnlyReactiveProperty<T>` exposes the latest value (`.CurrentValue`) and updates without a public value setter. It is a good **public surface for Model-owned mutable state**.
- `Observable<T>` exposes a sequence of notifications. It does not inherently promise a current value. It suits derived streams and signals.
- In R3 a read-only reactive property is also an observable, so presenting an existing reactive property through the narrower `Observable<T>` type does not change the behavior of that concrete source; it removes the current-value contract from its public API.

Illustrative pattern:

```csharp
using R3;

public sealed class CounterModel : IDisposable
{
    private readonly ReactiveProperty<int> _count = new(0);
    public ReadOnlyReactiveProperty<int> Count => _count;

    // Invoke through the owning service or an explicit permitted operation.
    public void Increment() => _count.Value++;

    public void Dispose() => _count.Dispose();
}
```

Prefer read-only exposure for shared state. This is an **API convention**, not a demand to write a wrapper and setter for every field; if a domain is small and service-owned mutation through a public `ReactiveProperty<T>` is deliberate, document the owner and avoid writes from presentation. Read-only exposure does not itself solve cross-model invariants or prevent a consumer from disposing a shared property; ownership rules still apply.

Do **not** wrap immutable configuration fields and every computed getter in reactive properties. A computed getter can remain plain C#; expose a derived observable only when consumers need change notifications.

### 3.2 Collections

Use `ObservableList<T>` (ObservableCollections) for dynamic collections that UI observes. A typical owner/service changes the list; a ViewModel observes add/remove/reset and maintains child ViewModels or presentational entries; the View binds only affected rows.

- Give domain entities stable identity, independent of list position.
- Define behavior for `Add`, `Remove`, `Reset`, and—where used—`Move`/`Replace`; avoid rebuilding all rows for one item change.
- Item field changes should come from item-level reactive properties or explicit replacement notifications, not implicit assumptions about collection events.
- Whoever creates a child ViewModel disposes it on removal/reset and on parent disposal.
- Treat publicly exposed mutable `ObservableList<T>` as an ownership boundary. Use a read-only collection projection if appropriate; otherwise enforce service-owned writes by convention.

## 4. Intent and operations

Both invocation styles are supported:

```text
ViewModel -> DomainService.TryPerform(...) -> Models
ViewModel -> VitalRouter command -> handler -> DomainService -> Models
```

Use direct calls for explicit one-to-one dependencies and operations needing immediate results, validation, or return values. Use VitalRouter for decoupled requests, cross-feature orchestration, milestones, and fan-out notifications. Do not put continuous values or per-frame state into commands. No service is obliged to expose both APIs.

A service that coordinates several Models should validate before mutation and keep critical synchronous transitions coherent. A full transaction framework is not required. A `busy`/`processing` flag may guard reentry, but **does not automatically buffer R3 notifications**; use a final domain notification, explicit gating, or batching only where observers genuinely require an all-at-once view. Await critical asynchronous operations and surface failures; reserve `.Forget()` for intentionally detached work with appropriate exception/cancellation handling.

## 5. Presentation patterns

### 5.1 Small, passive widget

A simple View with serialized UI references may be configured by a parent View/Composer. No extra ViewModel, interface, or Composer is necessary.

### 5.2 Reactive widget

A View directly binds an owned ViewModel's properties and button handlers and disposes its **own** subscriptions on unbind/destroy. It must not dispose a ViewModel owned by its parent or the caller.

### 5.3 Screen/window

An rWS window prefab has `WindowComposer<TViewModel>` for type resolution and lifecycle integration. The Composer may contain binding code or call `ScreenView.Bind(viewModel, token)`; the second arrangement is recommended when the View owns dynamic widget creation. Only one component should own each subscription and each ViewModel's disposal.

### 5.4 Interfaces and factories

- Add an `I...ViewModel` only when there is real substitution/testing or a shared contract. Don't create one by default.
- Use VContainer to resolve presentation dependencies; use factories for a ViewModel that requires a selected runtime entity or ID.
- Keep manual construction of complex ViewModels in one responsible factory when repeated; don't move every trivial `new` call into a factory.
- Avoid using a ViewModel as a general-purpose navigation service. Let a window-level Composer or a dedicated presentation navigation helper own routing when that avoids event-forwarding loops.

## 6. Lifetime and disposal contract

| Resource | Owner | Disposal point |
| --- | --- | --- |
| Game/session Model and service | The owning VContainer session scope | Session shutdown |
| UI window root and its bindings | rWS/window Composer | Window close/destruction or explicit unbind |
| Window-created ViewModel | Window opening owner, if its ownership is transferred explicitly | Window close, after binding subscriptions are removed |
| Caller-supplied ViewModel | Caller, unless explicit ownership transfer is agreed | Caller-defined |
| Child ViewModel | Parent ViewModel/factory that creates it | Row removal/reset or parent disposal |
| View subscriptions | View or Composer that created them | Rebind/unbind/destroy |
| Service command subscription | Subscribing service or owning scope | Service/scope disposal |

Do not let the same ViewModel be disposed independently by both Composer and a separate service. Support repeated bind/unbind safely (including pooled objects); cancellation of an async window operation must not leave subscriptions or tasks behind. Use `CompositeDisposable`, cancellation tokens, and explicit ownership rather than assuming Unity `OnDestroy` covers every lifetime.

## 7. Dependency injection guidelines

- Split a large composition root into **installers/registration modules** organized by responsibility. An installer is not a VContainer scope.
- Use real parent/child `LifetimeScope`s only for different lifetimes (for example a gameplay session and its UI scene), **not** one scope per feature.
- A child resolves parent registrations; a parent must not depend on a child-only service. Move cross-boundary behavior to a parent-level contract or re-evaluate scope placement.
- Prefer a VContainer entry point/explicit startup coordinator over registering services whose constructors have hidden side effects and then forcing `.Resolve()` in a build callback.
- Prefer constructor injection for ordinary services. Window root components can be registered or injected by the UI scope; scene object placement is not a concern of the domain layer.
- Do not register a `Lifetime.Singleton` reflexively; choose session scope, UI scope, or window ownership intentionally.

Further initialization rules and optional snapshot persistence are in [DataFlow.md](DataFlow.md).

## 8. Conformance checklist

- A service can be tested without constructing UI views or windows.
- A ViewModel can be tested without `Button`, `TMP_Text`, or `VisualElement` objects.
- Opening and closing the same window repeatedly does not increase live subscriptions.
- Adding/removing a collection item updates only the appropriate widgets, and removed child ViewModels are disposed.
- A cancelled operation does not leave its owner in an impossible state.
- An rWS window uses a Composer as the root adapter; simple nested widgets need no extra Composer.
- The reusable module does not reference consuming-game assemblies or content definitions.

## 9. Related documents

- [DataFlow.md](DataFlow.md) — initialization, ownership, VContainer scopes and optional persistence.
- [ReactiveWindowSystem.md](ReactiveWindowSystem.md) — reusable window routing and modal lifecycle.
