# Reactive MVVM (rMVVM) Architecture

**Scope:** Pure C# architecture & UI presentation patterns. For state initialization and persistence, see [DataFlow.md](DataFlow.md). For UI navigation routing, see [ReactiveWindowSystem.md](ReactiveWindowSystem.md).

---

## 1. Core Philosophy

rMVVM is designed for small teams prioritizing rapid iteration, minimal boilerplate, and maintainable UI code. It uses **VContainer** for dependency injection and **R3** for reactive state, enabling clean separation of concerns without heavyweight frameworks.

**Key Objectives:**

* **Rendering-Agnostic Frontend:** Logic remains identical whether the underlying UI is uGUI (Canvas) or UI Toolkit (UITK).
* **Low Boilerplate:** Minimal files to stand up a screen. Avoid over-engineering pure presentation (use Dumb Views for simple components instead of full MVVM triads).
* **Strict DI Boundaries:** VContainer handles pure C#→C# wiring (Services → ViewModels). Composers bridge C# → Unity, never mixing the two concerns.
* **State vs. Intent Division:** Continuous state flows through R3 observables; discrete one-time actions flow through VitalRouter commands (see §2).

---

## 2. The Golden Rule: State vs. Intent

To prevent GC overhead and architectural confusion:

* **Use VitalRouter for discrete intents & milestones:** One-time actions only (`StartActivityCommand`, `ActivityCompletedEvent`). Never for every-frame updates.
* **Use R3 for continuous state:** Values that change frequently (progress bars, health counters, UI refreshes). Mutate `ReactiveProperty.Value` directly; subscribers react automatically.

---

## 3. The Four Pillars

### 3.1 Model (Domain Layer)

* **Role:** Core game state and business logic. Completely unaware of Unity and UI.
* **Ownership:** Populated at application startup via VContainer. See [DataFlow.md: Initialization](DataFlow.md#1-initialization-the-origin-of-truth).
* **State exposure:** Exposes read-only `R3.ReactiveProperty<T>` observables. Mutated exclusively through Commands (VitalRouter) or Domain Services.
* **Tech:** Pure C#. Registered as Singletons in VContainer `LifetimeScope`.
* **Example:** A domain `BuildingModel` wraps game state (health, level, resources) in `ReactiveProperty<>` fields. The model is agnostic of whether a UI exists.

### 3.2 ViewModel (Presentation State)

* **Role:** Bridge between Domain and View. Observes the Model and transforms raw data into UI-ready formats.
* **UI Logic:** Owns all presentation-specific logic — formatting (120 sec → "02:00"), calculating progress (0.0–1.0), filtering lists, determining button interactability. Does **not** execute business logic.
* **Rules:** Contains zero references to `UnityEngine.UI` or `UnityEngine.UIElements`. Easily unit-testable. Injected by VContainer as Transient (each screen/row gets its own instance).
* **Tech:** Pure C#, `R3.ReactiveProperty<T>`, `R3.Observable<T>`.
* **Example:** A UI screen observes the domain's `Building` model and exposes a `ReadOnlyReactiveProperty<string> HealthDisplay` that transforms raw HP into "45/100 HP" text. The ViewModel requests domain services (e.g., `BuildingService`) via constructor injection — VContainer resolves them automatically.

### 3.3 Composer (Binder & Lifecycle Owner)

* **Role:** C#↔Unity bridge. Receives a fully-constructed ViewModel and introduces it to the View.
* **Ownership:** Owns the UI lifecycle. Manages `destroyCancellationToken` and disposes R3 subscriptions when the GameObject is destroyed, preventing memory leaks.
* **Rules:** The *only* layer allowed to reference both pure C# ViewModels and Unity UI components. Does **not** instantiate Domain Services or execute logic.
* **Tech:** `MonoBehaviour`. Typically sealed, non-generic base class `WindowComposer` + generic `WindowComposer<TViewModel>`.
* **Example:** A Composer wires the ViewModel's `HealthDisplay` to a `TextMeshProUGUI` component and subscribes to it:
  ```csharp
  _viewModel.HealthDisplay.Subscribe(text => _healthText.text = text).AddTo(_disposables);
  ```

### 3.4 View (Dumb Presentation)

* **Role:** Pure visual shell.
* **Rules:** Contains absolutely zero logic. Exposes references to UI elements (`TMP_Text`, `Button`, etc.) or simple rendering methods. Driven entirely by the Composer.
* **Tech:** `MonoBehaviour` + uGUI/UITK components.

---

## 4. Dependency Injection (VContainer) Boundaries

VContainer strictly separates how logic is *built* from how it's *bound to the screen*.

### C# → C# (VContainer's job)

`ViewModel` constructors declare their dependencies. VContainer inspects the constructor, resolves every dependency (other services, models, factories), and returns a fully hydrated instance:

```csharp
public class SomeViewModel
{
    public SomeViewModel(SomeService service, SomeModel model)
    {
        // VContainer automatically found and injected service and model
    }
}
```

For runtime data + static dependencies, VContainer supports factory delegates:

```csharp
// In GameLifetimeScope:
builder.RegisterFactory<BuildingModel, BuildingDetailsViewModel>(
    resolver => building => new BuildingDetailsViewModel(building, resolver.Resolve<...>())
);

// Later, in a caller:
var vm = resolver.Resolve<BuildingModel, BuildingDetailsViewModel>(specificBuilding);
```

### C# → Unity (Composer's job)

VContainer does not know about Prefabs or UI components. The Composer takes the DI-built ViewModel and wires it to the View manually. This keeps rMVVM framework-agnostic and doesn't force DI to make UI decisions.

---

## 5. UI Assembly Patterns

### 5.1 Dumb Views

For simple, repeating elements (e.g., a resource icon + count pair, a status bar).

* **Implementation:** A simple View class exposing public fields or rendering methods. Prefix with `Dumb` to clarify its role (e.g., `DumbResourceRowView`).
* **Usage:** The parent View directly calls the Dumb View's methods. No dedicated ViewModel.
* **Benefit:** Zero boilerplate. No MVVM triad needed for a single label.

### 5.2 Smart Widgets (Nested MVVM)

For components with independent behavior, dynamic state, or their own logic (e.g., an activity countdown timer, a personnel row with sub-actions).

* **Implementation:** Its own View and ViewModel.
* **Usage:** Parent ViewModel holds an `ObservableList<ChildViewModel>`. Parent View instantiates child Prefabs and passes their ViewModels to their Composers.
* **Benefit:** Encapsulation. Each widget is independently testable and composable.

### 5.3 Universal Exchangeability (Interfaces)

To enable complete UI replacement without touching logic:

* Views depend on **interfaces** rather than concrete ViewModels (e.g., `IPersonnelRowViewModel` instead of `PersonnelRowViewModel`).
* This allows swapping uGUI for UITK, or even a web UI, with zero logic changes.

---

## 6. Architecture in Practice

The layering flows like this:

1. **On Boot:** VContainer reads ScriptableObject configs and populates Domain Models. See [DataFlow.md: Initialization](DataFlow.md#1-initialization-the-origin-of-truth).
2. **UI Request:** Navigation request arrives (user click, game event, etc.). See [ReactiveWindowSystem.md](ReactiveWindowSystem.md) for how rWS routes it.
3. **ViewModel Construction:** rWS asks VContainer to resolve the requested `ViewModel`. VContainer inspects its constructor, resolves dependencies, returns the instance.
4. **Binding:** rWS instantiates the Prefab and passes the ViewModel to its Composer. The Composer wires ViewModel observables to View UI elements.
5. **Runtime:** Model mutates (Commands or Services). ViewModel observes and transforms. View subscribes and renders.
6. **Cleanup:** When the Prefab is destroyed, the Composer disposes subscriptions via `destroyCancellationToken`.

---

## 7. Common Pitfalls

* **Putting UI logic in the Model:** Models should know nothing of UI formats, button states, or lists. All of that belongs in the ViewModel.
* **Injecting Views into ViewModels:** If a ViewModel needs a reference to UI components, move that wiring to the Composer instead. ViewModels should be testable without Unity.
* **Missing disposal in Composers:** Every R3 subscription must be added to the Composer's `CompositeDisposable` and disposed in `OnDestroy`. Memory leaks otherwise.
* **Using Commands for continuous state:** Commands are discrete, one-time events. For values that change every frame, use R3 `ReactiveProperty` instead.

---

## 8. See Also

* [DataFlow.md](DataFlow.md) — How data is initialized and persisted.
* [ReactiveWindowSystem.md](ReactiveWindowSystem.md) — How UI windows are routed and stacked.
