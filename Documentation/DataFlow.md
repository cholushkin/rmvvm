# Data Flow & Persistence Architecture

**Scope:** How domain state enters and leaves the application. For architecture basics, see [rMVVM.md](rMVVM.md). For UI navigation, see [ReactiveWindowSystem.md](ReactiveWindowSystem.md).

---

## 1. Initialization — The Origin of Truth

Data enters the application during VContainer `LifetimeScope` initialization, replacing manual Bootstrapper scripts. Two primary sources:

### 1.1 Static Configurations (ScriptableObjects)

Designer-tuned data containers hold baseline values and databases (e.g., `StartingResourcesConfig`, `BuildingDatabase`, `PricingDatabase`).

* **In VContainer:** Registered as instances during DI setup:
  ```csharp
  builder.RegisterInstance(_buildingDatabase);
  builder.RegisterInstance(_startingConfig);
  ```
* **Into Models:** VContainer injects them into Domain Model constructors, which wrap them in `ReactiveProperty<T>`:
  ```csharp
  public class ResourceModel
  {
      public ReactiveProperty<int> Credits { get; }
      
      public ResourceModel(StartingResourcesConfig startingConfig)
      {
          Credits = new(startingConfig.StartingCredits);
      }
  }
  ```

### 1.2 Loaded Save Data (DTOs)

If a valid save file exists, its deserialized state (Data Transfer Objects) is used instead of the defaults. The flow:

1. Boot sequence checks `Application.persistentDataPath` for a save file.
2. If found, deserializes it (e.g., via `Newtonsoft.Json`) into lightweight DTO objects (`ResourceSaveData`, `SettlementSaveData`).
3. VContainer passes the DTOs to the Models:
   ```csharp
   var loadedSaveData = LoadSaveFile();
   var resourceModel = new ResourceModel(loadedSaveData.ResourceState);
   builder.RegisterInstance(resourceModel);
   ```
4. Models initialize their `ReactiveProperty<T>` fields from the DTOs.
5. UI boots normally, binding to the restored, live values — no special case code.

If no save exists, the ScriptableObject defaults are used (§1.1).

---

## 2. Runtime — The Reactive Flow

Once Models are initialized, they are the single source of truth for the application.

* **Mutations:** Domain Services mutate Models reactively. VitalRouter Commands trigger service methods, which update `ReactiveProperty.Value`:
  ```csharp
  private void HandleSpeedUpActivity(SpeedUpActivityCommand cmd, PublishContext context)
  {
      var cost = _pricingDb.GetSpeedUpCost(cmd.Activity.RemainingSeconds.Value, cmd.Activity.DurationSeconds);
      if (_resources.Credits.Value >= cost.Credits)
      {
          _resources.Credits.Value -= cost.Credits;  // Direct assignment triggers R3 subscribers
          cmd.Activity.RemainingSeconds.Value -= 60;
      }
  }
  ```
* **Observation:** ViewModels subscribe to Model properties and expose derived state (see [rMVVM.md: The Four Pillars](rMVVM.md#3-the-four-pillars)):
  ```csharp
  public class ActivityRowViewModel
  {
      public ReadOnlyReactiveProperty<string> TimeRemaining { get; }
      
      public ActivityRowViewModel(ActivityModel activity)
      {
          TimeRemaining = activity.RemainingSeconds
              .Select(seconds => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"))
              .ToReadOnlyReactiveProperty();
      }
  }
  ```
* **Rendering:** Views subscribe to ViewModel observables and render (Composer wiring, see [rMVVM.md: The Composer](rMVVM.md#33-composer-binder--lifecycle-owner)).

---

## 3. Persistence — The Snapshot Pattern

`ReactiveProperty<T>` contains event subscriptions and UI delegates, so it cannot be cleanly serialized by standard JSON or binary formatters. Persistence requires a different approach: **serializable snapshots (DTOs)**.

### 3.1 State DTOs (Plain Old C# Objects)

Define lightweight, serializable POCO objects composed entirely of primitives and plain lists:

```csharp
[Serializable]
public class ResourceSaveData
{
    public int Credits;
    public int Supplies;
    public int Food;
}

[Serializable]
public class SettlementSaveData
{
    public ResourceSaveData Resources;
    public List<BuildingSaveData> Buildings = new();
    public List<PersonnelSaveData> Personnel = new();
}
```

These contain **only** the data needed to reconstruct the Models on load — no event subscriptions, no UI state, no transient runtime data.

### 3.2 Save Flow (Model → DTO → Disk)

1. Player clicks "Save Game" or the save trigger fires (e.g., on activity completion).
2. A `SaveService` extracts current values from Models:
   ```csharp
   var saveData = new SettlementSaveData
   {
       Resources = new ResourceSaveData
       {
           Credits = _resourceModel.Credits.Value,
           Supplies = _resourceModel.Supplies.Value,
           Food = _resourceModel.Food.Value
       },
       Buildings = _settlement.Buildings
           .Select(b => new BuildingSaveData { /* ... */ })
           .ToList()
   };
   ```
3. DTOs are serialized to JSON (or binary) and written asynchronously to `Application.persistentDataPath`.
4. Optional: Emit an event so UI can show "Saved ✓" or analytics can log the save.

### 3.3 Load Flow (Disk → DTO → Model)

This happens during boot, before the UI is created (see §1.2). The sequence:

1. Boot code attempts to read the save file.
2. If found and valid, deserialize it into DTOs.
3. Pass the DTOs to the Models (as if they were ScriptableObject configs):
   ```csharp
   var loadedSaveData = LoadSaveFile();
   var resourceModel = new ResourceModel(loadedSaveData.ResourceState);
   var settlementModel = new SettlementModel(loadedSaveData, _buildingDatabase);
   builder.RegisterInstance(resourceModel);
   builder.RegisterInstance(settlementModel);
   ```
4. Models initialize their `ReactiveProperty<T>` fields from the DTO values.
5. The rest of the boot sequence proceeds normally.

**No UI code knows about DTOs.** ViewModels and Views see reactive Models and work exactly as in a fresh game. The DTO→Model hydration is purely an initialization-time concern.

---

## 4. Best Practices

* **DTOs are simple:** No logic, no VContainer, no dependencies. They're data containers only. This makes them robust for serialization and easy to version.
* **Models are reactive:** Always wrap primitive data in `ReactiveProperty<T>` so UI and other systems can subscribe and react to changes.
* **Services mutate, don't Views:** Only Domain Services should modify Models. Views and ViewModels observe, never write (except for UI state like "is this dropdown expanded", which isn't persisted anyway).
* **Save often, think about timing:** Frequent auto-saves (e.g., on every activity completion, every building upgrade) prevent catastrophic loss. Manual save before risky operations is a UX pattern, but shouldn't be the only save mechanism.
* **Test with fresh and loaded saves:** Always test both a fresh game *and* loading a save to catch serialization bugs and Model initialization edge cases.

---

## 5. See Also

* [rMVVM.md](rMVVM.md) — Architecture of the Model, ViewModel, Composer, View layers.
* [ReactiveWindowSystem.md](ReactiveWindowSystem.md) — How UI windows are navigated and routed while the game state flows.
