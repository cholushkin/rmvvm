# Data Flow & Persistence Architecture

## 1. Initialization (The Origin of Truth)
Data enters the application during the VContainer `LifetimeScope` initialization phase, explicitly replacing manual Bootstrapper scripts. There are two primary sources for initial state:

* **ScriptableObjects (Designer-Tuned):** Static data containers (e.g., `StartingResourcesConfig`, `BuildingDatabase`) hold primitive values tuned by designers. VContainer registers these instances and injects them directly into the Domain Models and Services.
* **Hardcoded Configurations (Prototyping):** Primitives defined directly within the `LifetimeScope` or Model constructors for rapid iteration before formal data structures exist.

Once injected into the Domain Models via DI, these raw primitives are wrapped in `ReactiveProperty<T>`. This establishes the mutable, observable single source of truth for the entire session.

---

## 2. Serialization (Saving & Loading Runtime State)
Because `ReactiveProperty<T>` contains event subscriptions and UI delegates, it cannot be cleanly serialized by standard JSON or Binary formatters. To persist game progress, runtime data must be strictly decoupled from its reactive wrappers.

### The Snapshot Pattern (DTOs)
Persistence is handled by creating lightweight, serializable snapshots of the domain state.

* **State DTOs:** Define Plain Old C# Object (POCO) Data Transfer Objects composed entirely of primitive types and plain lists (e.g., `ResourceSaveData`, `SettlementSaveData`).
* **Save Flow (Model → DTO → Disk):**
  1. A `SaveService` triggers a save routine.
  2. The Domain Models extract their current raw values (`property.Value`) and map them into the state DTOs.
  3. The DTOs are serialized (e.g., via `Newtonsoft.Json` or `MemoryPack`) and written to disk asynchronously.
* **Load Flow (Disk → DTO → Model):**
  1. During application launch, the `LifetimeScope` or a dedicated `InitializationService` attempts to read and deserialize the save file.
  2. If a valid save exists, VContainer uses the loaded DTOs (instead of the default ScriptableObject starting configs) to populate the Domain Models.
  3. The Models initialize their `ReactiveProperty<T>` wrappers with the loaded state.
  4. The UI is spawned via rWS, binding to these restored, live values as normal.