# Reactive MVVM (rMVVM) Architecture Guidelines

## 1. Core Philosophy
The Reactive MVVM (rMVVM) architecture is designed for small teams prioritizing rapid iteration, minimal boilerplate, and highly maintainable UI code. It utilizes **VContainer** for lightweight, allocation-free Dependency Injection, replacing fragile manual wiring and bootstrappers without introducing the bloat of "heavy" DI frameworks.

**Key Objectives:**
* **Agnostic Front-End:** Logic remains identical whether the underlying rendering technology is UI Toolkit (UITK) or Canvas (uGUI).
* **Low Boilerplate:** Minimal files required to stand up a screen. Avoid over-engineering pure presentation elements (use Dumb Views for simple icon/text pairs instead of full MVVM triads).
* **Structured Dependency Injection:** VContainer strictly handles the C#-to-C# object graph (Services, Models, ViewModels). Unity MonoBehaviours (Views) are never injected directly via DI, preserving explicit data flow.

### The Golden Rule of State vs. Intent
To prevent architectural misuse and garbage collection overhead, state and messaging are strictly divided:
* **Use VitalRouter for Intents & Milestones:** Routing is exclusively for discrete, one-time actions (e.g., `StartActivityCommand`, `TakeDamageCommand`, `ActivityCompletedCommand`). **Never** use it for every-frame updates.
* **Use R3 for Continuous State:** For values changing every frame (e.g., a progress bar filling up, physical movement), domain loops mutate the `ReactiveProperty.Value` directly. R3 efficiently mirrors this continuous change to the UI.

---

## 2. The Four Pillars
The architecture is divided into four distinct layers to enforce separation of concerns.

### 2.1 Model (Domain)
* **Role:** The core game state and business logic. Completely unaware of Unity and UI.
* **Population:** Models are populated at application startup. The DI Container (VContainer) passes static configurations (ScriptableObjects) or deserialized save data (DTOs) into the Model's constructor before any UI is loaded.
* **Rules:** Mutated exclusively through `VitalRouter` Commands (for milestones) or Domain Services (for continuous state). Exposes read-only state via `R3` observables.
* **Tech:** Pure C#. Registered as Singletons in VContainer `LifetimeScope`.

### 2.2 ViewModel (Presentation State)
* **Role:** The bridge. Observes the Model and transforms raw data into UI-ready formats.
* **UI Logic:** This layer owns all UI-specific logic. It formats strings (e.g., converting 120 seconds into "02:00"), calculates progress floats (0.0 to 1.0), filters lists, and dictates whether buttons should be interactable based on the Domain state. It does *not* execute business logic.
* **Rules:** Contains zero references to `UnityEngine.UI` or `UnityEngine.UIElements`. Easily unit-testable. Requested from VContainer (Transient or Scoped) with all dependencies injected automatically via constructor.
* **Tech:** Pure C#, `R3.Observable`, `R3.ReactiveProperty`.

### 2.3 Composer (The Binder & Lifecycle Owner)
* **Role:** The C#-to-Unity bridge. Receives fully-constructed ViewModels and introduces them to the View.
* **Ownership:** The Composer owns the UI lifecycle. It manages the `destroyCancellationToken` and is responsible for disposing of R3 subscriptions and nested ViewModels when the Unity `GameObject` is destroyed, preventing memory leaks.
* **Rules:** This is the *only* layer allowed to know about both the pure C# ViewModel and the Unity View components. It does *not* instantiate Domain Services or execute logic.
* **Tech:** `MonoBehaviour`.

### 2.4 View (Dumb Presentation)
* **Role:** The visual shell.
* **Rules:** Contains absolutely zero logic. Exposes references to UI elements (e.g., `TMP_Text`, `Button`) or defines basic rendering methods. Driven entirely by the Composer.
* **Tech:** `MonoBehaviour`, uGUI / UI Toolkit.

---

## 3. Dependency Injection (VContainer) Boundaries
To maintain absolute modularity, we strictly separate how logic is built versus how it is bound to the screen.

* **VContainer handles the "C# to C#" Boundary:** It wires Services to ViewModels. `ActivitiesViewModel` simply requests `ActivityService` in its constructor, and VContainer resolves it.
* **Composers handle the "C# to Unity" Boundary:** VContainer does not know what a Prefab or a `TextMeshProUGUI` component is. The Composer takes the VContainer-built ViewModel and wires its `R3` properties to the Unity components.

**Runtime Data with Static Services:** When a ViewModel requires both static DI services and specific runtime data (e.g., clicking a specific `BuildingModel`), VContainer is used to register a Factory delegate (`Func<BuildingModel, BuildingDetailsViewModel>`). The caller provides the runtime data, and VContainer automatically injects the rest.

---

## 4. UI Assembly Patterns

### Dumb Views (Minimal Boilerplate)
For simple repeating elements (e.g., a resource icon and text).
* **Implementation:** Create a simple View class exposing public fields or a rendering method. Prefix the class with `Dumb` to clarify its architectural role (e.g., `DumbNavButtonView`).
* **Usage:** The parent View directly updates the Dumb View. No dedicated ViewModel is created.

### Smart Widgets (Nested MVVM)
For widgets with independent behavior, dynamic state, or their own data fetching needs (e.g., an Activity row with a live countdown).
* **Implementation:** The widget receives its own View and ViewModel.
* **Usage:** The parent ViewModel holds an `ObservableList` of child ViewModels. The parent View dynamically instantiates child View Prefabs and passes the child ViewModels to their Composers.

### Universal Exchangeability (Interfaces)
To make widgets universally reusable, Views should depend on **Interfaces** rather than concrete ViewModels (e.g., `IPersonnelRowViewModel`). This allows complete UI replacement (e.g., swapping uGUI for UITK) without touching the logic.

---

## 5. Directory Structure
To prevent namespace pollution and logic leaking into the UI, the project hierarchy strictly reflects the architectural boundaries.

```text
Runtime/
├── Domain/                 # Pure C# (No Unity references)
│   ├── Models/             # State wrappers (SettlementModel.cs)
│   └── Services/           # Business logic (ActivityService.cs)
├── Infrastructure/         # VContainer Setup & Save Systems
│   └── GameLifetimeScope.cs
├── Messaging/              # VitalRouter Commands
│   └── Commands/
└── UI/                     # Presentation Layer
    ├── WindowSystem/       # Core rWS scripts (WindowService.cs)
    ├── SharedDumbViews/    # Globally used Dumb Views (DumbResourceView.cs)
    ├── Widgets/            # Smart Widgets (Nested MVVM Triads)
    │   └── ActivityRow/
    │       ├── ActivityRowViewModel.cs
    │       ├── ActivityRowComposer.cs
    │       └── ActivityRowView.cs
    └── Screens/            # Full Window Triads (Routed via rWS)
        └── Activities/
            ├── ActivitiesViewModel.cs
            ├── ActivitiesComposer.cs
            └── ActivitiesView.cs