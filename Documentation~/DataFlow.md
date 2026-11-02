# Data Flow, Initialization & Persistence — Reusable Architecture

**Status:** Target architecture contract for a reusable Unity module  
**Related:** [rMVVM.md](rMVVM.md) · [ReactiveWindowSystem.md](ReactiveWindowSystem.md)

> **Module boundary:** This document defines reusable data-flow conventions. It must not know about any consuming game's specific models, balance parameters, gameplay systems, or configuration assets. A game supplies its own schemas, authoring assets, initialization, and service registrations.

## 1. Sources of data

Keep these concepts separate:

| Kind | Examples | Expected mutability | Reactive? |
| --- | --- | --- | --- |
| **Definitions/configs** | ScriptableObject databases, curves, thresholds, starting parameters | Treat as immutable during a running session | Usually no |
| **Session state** | Owned entities, quantities, flags, timers, activity status | Changes during play | Observe only where useful |
| **Derived state** | Calculated capacities, validity flags, formatted values | Calculated from source state | On demand; derived reactive stream if required |
| **Transient presentation state** | Expanded panels, selection highlight, hover | UI lifetime only | Optional; not automatically persistent |
| **Optional save snapshot** | Serializable reconstruction records | Created/read during save/load | No |

ScriptableObjects are a Unity authoring mechanism, not runtime storage. They may contain small pure calculations closely related to their configuration. They should not be mutated as a method of recording a session's progress.

## 2. Initialization: new session and restored session

A consuming game owns the boot sequence. An illustrative pipeline:

```text
Load/register static configurations
        |
Validate identifiers, references, and required assets
        |
Select initial-state source
   /                   \
New-session defaults    Optional loaded snapshot (validate/migrate)
   \                   /
Create/hydrate runtime Models
        |
Resolve and initialize Services + scheduler
        |
Create UI scope / open initial windows
```

- Loading serialized state, if implemented, happens before UI observes the initial state.
- New-session state and restored state should produce the **same Model APIs**; no special cases in ViewModels.
- Prefer explicit factories or initialization services for spawning initial populations/random data; don't hide random world generation in constructors that also run when rehydrating.
- Models may reference immutable ScriptableObject definitions, Unity value types, or assets when useful; they should not depend on UI components or a particular rendering system.
- Avoid mutating ScriptableObject assets at runtime. A DTO from an optional save is not a ScriptableObject.
- Define how asynchronous preparation finishes before dependent entry points start. Don't assume DI registration order alone sequences side-effectful constructors.

## 3. Runtime state and mutations

Models are the authoritative source for session state. The owning service, or an explicit Model operation, changes it. The ViewModel observes it and derives presentation values; the View/Composer renders them.

```text
User intent -> ViewModel -> Domain Service (directly or via VitalRouter)
                               |
                       validate + mutate Models
                               |
                       R3 / ObservableCollections
                               |
                      ViewModel projections
                               |
                       View / Composer
```

- `ReactiveProperty<T>`: writable to its owner; expose `ReadOnlyReactiveProperty<T>` where beneficial.
- `Observable<T>`: stream without a required current-value contract; use for signals and derived streams.
- `ObservableList<T>`: dynamic list observation; ensure identity, write ownership, item-level change handling and child-lifetime cleanup.
- Not every scalar needs to be reactive; ordinary fields and computed getters are valid when change notifications aren't needed.
- Domain rules (eligibility, constraints, transitions) belong in Models/services, not duplicated in ViewModels.
- Direct domain-service calls and VitalRouter commands may coexist. Use VitalRouter for decoupled intent/milestones, not as a mandatory hop for every button.

### 3.1 Coordinated operations

A multi-model operation needs one logical owner that checks all preconditions and arranges updates in a controlled order. Example: a generic operation that spends a resource, changes a target status, and starts a timed task.

- Validate target, availability and affordability before mutating.
- Prevent duplicate execution where needed using an operation/status flag or service guard.
- Do not mistake a guard flag for an R3 transaction: subscribers may observe intermediate writes. Only add gating, batched publication or an explicit final completion signal when the situation requires it.
- Keep important async side effects awaited or with explicit completion/error handling. Fire-and-forget is acceptable for noncritical notifications with supervised errors.
- Cancellation and failure cleanup must release any resources or reservations owned by the operation.

## 4. Time and long-running activities

Treat gameplay time as a domain concern, separate from UI repaint frequency.

- Prefer one authoritative game-time source and scheduler for time-bound work; features register operations rather than each operating their own `UniTask.Delay(1 second)` loop.
- Represent activities with stable IDs, owner IDs, type/status and start/due times or equivalent canonical timing data. Pick a consistent in-game time unit.
- Distinguish **timer elapsed**, **completion side effects processed**, and **claimed/acknowledged** where the domain needs separate steps.
- Use explicit pause/resume behavior in the clock/scheduler. A UI screen may request pausing through a game-owned policy, but a generic window system must not define gameplay pause rules.
- Recompute a countdown/progress for display from clock state; don't run unnecessary per-row frame subscriptions unless smooth presentation requires them.
- If saved later, store enough canonical timing/status data to reconstruct pending operations. A running task is gameplay state, not disposable UI state.

## 5. VContainer scopes and installers

A practical default is a **session scope** and a **child UI scope**, with an optional persistent **application scope** when there is a real lifetime above sessions.

```text
Optional ApplicationLifetimeScope (persistent systems)
        |
SessionLifetimeScope (runtime Models, services, scheduler, commands)
        |
UILifetimeScope (window routing, ViewModels, UI scene components)
```

- Each scope is a **lifetime boundary**. Don't make sibling scopes for tightly collaborating features just to group registrations.
- Divide registrations inside a scope into installers such as `ConfigInstaller`, `DomainInstaller`, `UIInstaller` and `ViewModelFactoryInstaller` (names are examples). Installers are simple organization, not DI containers.
- Parent registrations can be resolved from children. Parent-level services must not depend on registrations existing only in a child scope.
- Use explicit VContainer entry points/startup orchestration for services that must subscribe or run at startup. Constructor side effects plus forced resolution are a warning sign.
- If consumers provide scene objects or assets, adapters/installation code belongs to the consuming game; don't put those serialized fields into the reusable architecture module.
- For VContainer lifetimes, `Scoped` expresses one instance per scope. `Singleton` is also tied to the registration's container; choose according to intended sharing and teardown, not naming alone.

## 6. Optional persistence: snapshot pattern

Persistence is an **opt-in game feature**. The reusable architecture offers conventions; it must not force a `SaveService` or save DTOs into every consumer.

When a game enables saves:

1. **Capture:** read authoritative Models at a safe, consistent point and create plain DTOs composed of stable IDs, scalars and serializable collections.
2. **Serialize:** write asynchronously with error handling and appropriate file safety (for example write-temp-then-replace).
3. **Restore:** deserialize, validate version and references, migrate when required, then hydrate Models before starting simulation and UI.
4. **Resume:** restore pending activities, statuses and canonical schedule data coherently. Do not blindly generate a new starting state during load.

Do not serialize R3 properties, ObservableCollections implementations, delegates, Unity scene references, or live ScriptableObjects as mutable session state. Persist stable definition IDs and resolve them against the available configuration catalog when loading. Keep the snapshot format versioned if/when persistence is implemented.

**No automatic offline progression is implied.** If a consuming game requires it, define its policy separately and apply it through domain initialization/time systems.

## 7. Ownership, integrity and validation

- Each state transition and mutable collection has a clear owner.
- Validate configuration IDs, duplicate entries, reference integrity, ranges, probabilities, and content prerequisites in editor-time tools when practical.
- Avoid creating duplicate authoritative representations of the same state (for example one subsystem says an entity is occupied while another says it is free).
- If a scalar is derived, calculate it instead of storing a second independently writable copy unless caching is justified.
- Services subscribing to VitalRouter must release subscriptions when their owning scope ends.
- Tests should cover fresh initialization, repeated UI opens/closes, list add/remove/reset, activity cancellation, and (when implemented) save/load restoration.

## 8. Acceptance checklist

- Core architecture code contains no consuming-game model names or rules.
- A fresh session can initialize without any UI scene objects.
- An optional restored session uses the same Model APIs as a new session.
- Domain services do not resolve dependencies from the child UI scope.
- Critical operations validate before mutating and do not leak reservations on failure.
- Closing the UI does not terminate or reset session-owned gameplay state.
- No unnecessary per-feature DI scopes, mandatory save system, mandatory global transactions, or compulsory command-bus wrapping.

## 9. Related documents

- [rMVVM.md](rMVVM.md) — Model/ViewModel/Composer/View patterns and disposal rules.
- [ReactiveWindowSystem.md](ReactiveWindowSystem.md) — generic UI navigation and modal lifecycle.
