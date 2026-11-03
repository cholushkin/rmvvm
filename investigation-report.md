# rMVVM Package — Investigation Report

Scope: full read of `Documentation~/*.md`, `README.md`, `package.json`, `Runtime/RMVVM.asmdef`, every `.cs` file under `Runtime/`, and the shipped sample assets (`DataAndSettings/`, `Prefabs/`). Cross-checked docs against actual code and against the git history (`git log`) of this UPM conversion.

---

## 1. What's actually in this package vs. what the docs describe

The three docs read as one coherent system, but they document **two different things at two different layers**, and only one of them actually ships in `Runtime/`:

* **`rMVVM.md`** and **`DataFlow.md`** describe a full MVVM architecture — `Model`, `ViewModel`, base `Composer`, VitalRouter commands, save/load DTOs, VContainer `LifetimeScope` registration patterns. **None of this exists in the package.** There is no base `Model`, no `IViewModel`, no VitalRouter reference anywhere in `Runtime/` (grep confirms zero hits), no sample `LifetimeScope`. These two docs are written from the vantage point of a *consuming game project* (they even say "`[Project Example]`" and reference `Game.Input.InputService` under `Assets/Game/Runtime/...`), not from this package's own source.
* **`ReactiveWindowSystem.md`** is the one doc that matches shipped code closely — `WindowService`, `WindowComposer`, `WindowConfig`, `WindowDatabase`, dialogs — all present and accounted for, including its own "Known Limitations" section (§10), which is accurate (verified below).

**Recommendation:** Either split `rMVVM.md`/`DataFlow.md` out of this package's `Documentation~` (they document a project-level convention, not this package's API) and replace them with "recommended usage patterns" framed explicitly as guidance for consumers, or trim them to only the parts this package actually ships (ViewModel/Composer/View — drop Model, VitalRouter, DataFlow entirely, or clearly box them as "not part of rMVVM, shown for context"). As written, a new consumer reading `README.md` → `rMVVM.md` first will expect base classes that don't exist.

---

## 2. Concrete bug: `ConfirmDialog.asset` points at the wrong prefab

This is a real, reproducible functional bug in the shipped sample content, not a style nit.

- `DataAndSettings/WindowConfigs/ConfirmDialog.asset` has:
  ```
  Prefab: {fileID: 3338513665433511246, guid: 01be5cba06a438c46a0ab55f9fd10089, type: 3}
  ```
- `guid: 01be5cba06a438c46a0ab55f9fd10089` is **`Prefabs/AlertDialog.prefab`**'s GUID (see `Prefabs/AlertDialog.prefab.meta`), and `fileID 3338513665433511246` is that prefab's root GameObject ("AlertDialog", carrying `AlertDialogComposer`).
- `Prefabs/ConfirmDialog.prefab` has its own distinct GUID (`864577181c6c223449298151b3fa2b48`) which is never referenced by any `WindowConfig` asset — it's orphaned.

**Effect:** calling `windowService.ShowConfirmAsync(...)` resolves a `ConfirmDialogViewModel`, but `WindowService.ExecuteShowAsync` instantiates the **AlertDialog** prefab (because that's what `ConfirmDialog.asset.Prefab` points to) and gets its `AlertDialogComposer`. `WindowComposer<TViewModel>.BindBoxed` then does `if (viewModel is not TViewModel ...)` — `ConfirmDialogViewModel` is not `AlertDialogViewModel`, so binding fails, logs `[WindowComposer] Binding failed...`, and **returns without binding**. The Confirm/Cancel buttons are never wired to `viewModel.Confirm()`/`Cancel()`, so `ConfirmDialogViewModel.ResultTask` never completes — `ShowConfirmAsync` **hangs forever** (or until the caller's own `CancellationToken` fires). This is the one feature a new consumer is most likely to try first (per `README.md`'s and `ReactiveWindowSystem.md`'s own usage example), and it's broken out of the box.

**Fix:** repoint `ConfirmDialog.asset`'s `Prefab` field to `ConfirmDialog.prefab`'s GUID (`864577181c6c223449298151b3fa2b48`) in the Unity inspector (don't hand-edit the fileID of the root GameObject by guesswork — re-save from the Inspector so Unity picks the correct root fileID for that prefab).

---

## 3. Namespace / branding leftovers from the pre-UPM monolith

The git history shows the UPM conversion (`ef836fc feat: convert to UPM package`) only touched packaging metadata (`package.json`, `CHANGELOG.md`, `Documentation~` rename) — it never touched the C# source. As a result:

* **Half the public API has no namespace at all** (sits in the **global namespace**): `WindowService`, `WindowConfig`, `WindowComposer` / `WindowComposer<T>`, `WindowBuilder` / `WindowBuilder<T>`. The other half is namespaced `Game.UI.WindowSystem` / `Game.UI.Dialogs`: `IWindowService`, `WindowDatabase`, `WindowStackChange`, `StackMode`, `IModalViewModel`, `IHardwareBackHandler`, both dialog ViewModels/Composers, both transition classes. This split is inconsistent and pollutes the global namespace for a package meant to be installed alongside arbitrary other game code — a consumer project defining its own `WindowService` or `WindowBuilder` type will collide.
* **`Game.UI.*`** is the *consuming project's* namespace, not a namespace rMVVM owns (the package is `com.qcat.rmvvm`). Nothing in the package is namespaced `QCat` or `RMVVM`.
* **`[CreateAssetMenu(menuName = "GameLib/UI/Window Config", ...)]`** (`WindowConfig.cs`) and `"GameLib/UI/Window Database"` (`WindowDatabase.cs`) still point at the old monolith's `GameLib/` menu root, not something under the package's own branding.
* **`DataAndSettings/ModuleManifest(RMVVM).asset`** is a leftover `GameLib` module-system artifact: its script reference is `GameLib::GameLib.GameLibModuleManifest`, a type that doesn't exist anywhere in this package (it lived in the monolith's `GameLib` assembly). It's now a dangling, non-functional asset — UPM packages don't need this kind of registry, `package.json` already is the manifest. It also has a stale `Version: 1.0.0a` field that has already drifted from `package.json`'s `1.0.1` and will keep drifting since nothing updates it.

**Recommendation:** pick one namespace for the whole package (e.g. `QCat.RMVVM` / `QCat.RMVVM.Dialogs`) and apply it consistently to every type, rename the `CreateAssetMenu` paths to something package-owned (e.g. `"rMVVM/Window Config"`), and delete `DataAndSettings/ModuleManifest(RMVVM).asset` (and its `.meta`) entirely — it has no consumer in this codebase (`WindowService` never reads it) and no purpose for a package distributed via UPM.

---

## 4. `README.md`'s dependency table doesn't match the asmdef

`README.md` states:

> RMVVM's Runtime assembly references 4 external assemblies (3 by GUID, 1 by name)

But `Runtime/RMVVM.asmdef` actually lists **5** references:
```
GUID:f51ebe6a0ceec4240a699833d6309b23
GUID:029c1c1b674aaae47a6841a0b89ad80e
GUID:6055be8ebefd69e48b49212b09b47b2f
LitMotion        <- by name
VContainer       <- by name
```
That's 3 by GUID + **2** by name, not "1 by name." The README's table (UniTask / LitMotion / R3 / VContainer) still lists only 4 rows total but `LitMotion` is clearly referenced by assembly *name* in the asmdef, not by GUID as the table's phrasing implies for "3 by GUID." Whoever wrote the table counted wrong, or the asmdef changed after the README's dependency note was written (plausible — `CHANGELOG.md` shows the `DOTween → LitMotion` swap happened in `a6021ba`/the `1.0.1` entry specifically changed the DOTween GUID reference into a `LitMotion` *name* reference, but the README text wasn't revisited after that).

**Also:** `package.json`'s `"dependencies"` only declares `jp.hadashikick.vcontainer`. If R3, UniTask, and LitMotion are resolvable as UPM packages (they are, via their own `package.json`s when installed through git URL or OpenUPM), they should be listed in `package.json.dependencies` too so Unity's Package Manager can report version conflicts/missing packages instead of consumers only finding out via a compile error. Right now VContainer is the only dependency UPM itself knows about; the other three are "tribal knowledge" documented only in prose in `README.md`.

**Recommendation:** regenerate the README table from the current `RMVVM.asmdef` (and note precisely which GUID maps to which package, which the current README hedges on with "Based on the `using` statements... these resolve to"), and add R3/UniTask/LitMotion to `package.json.dependencies` if their UPM package names are known and stable.

---

## 5. `StackMode.Clear`'s XML doc contradicts its own implementation

`StackMode.cs`:
```csharp
/// Destroys all active windows in the container before showing the new one.
Clear
```
But `WindowService.ExecuteShowAsync`:
```csharp
if (stackMode == StackMode.Replace || stackMode == StackMode.Clear)
{
    await HideAsync(targetContainer, cancellationToken);   // pops exactly ONE window
}
```
`HideAsync(string containerId, ...)` only ever pops the single topmost window of that container. `Clear` today behaves **identically** to `Replace`. `ReactiveWindowSystem.md` §4/§10 is honest about this ("⚠️ Currently identical to `Replace`"), which is good — but the enum's own XML doc comment ("Destroys *all* active windows") actively lies to anyone reading it from IntelliSense/IDE tooltips rather than the markdown docs. This is the kind of doc/code split that's easy to trust by accident since it's *inside the source file*.

**Recommendation:** either implement real "pop everything in this container" semantics for `Clear` (loop `HideAsync(containerId)` until the list is empty, as the doc already suggests as a workaround), or fix the XML doc comment to match current behavior and point at the markdown's "Known Limitations" note.

---

## 6. Composer `ShowAsync`/`HideAsync` have no re-entrancy guard

`WindowComposer.PlayTransitionAsync` sets `IsAnimating = true` for the duration of a transition, but nothing prevents `ShowAsync()` and `HideAsync()` from being invoked concurrently on the same composer instance. Concretely: if `StackMode.Replace`/`Clear` hides the *current* topmost window while that window's own show-transition is still in flight (e.g. rapid double-navigation, or `HideAsync` triggered by a cancellation token firing mid-show), you get two overlapping `PlayTransitionAsync` calls racing on the same `IWindowTransition`/`CanvasGroup`:

- Both calls independently snapshot/restore `canvasGroup.interactable` via their own `originalInteractable` local — whichever `finally` runs last wins, so the restored value can be stale depending on timing.
- For `LitMotionWindowTransition`, two motions animate the same `CanvasGroup.alpha` concurrently (one driving it toward 1, the other toward 0) — visibly flickery, and the final alpha depends on which motion's last tween write wins, not on which operation (show or hide) was semantically "supposed" to win.

**Recommendation:** guard `ShowAsync`/`HideAsync` with a simple `_isTransitioning` check (ignore/queue a call that arrives while one is already in flight), or at minimum cancel any in-flight transition before starting the opposite one (e.g. keep a `CancellationTokenSource` per composer that gets re-created/cancelled at the start of each `ShowAsync`/`HideAsync`).

---

## 7. `WindowService._flowTokens` grows unbounded and isn't disposed on teardown

`GetOrCreateFlowToken(flowId)` / `CancelFlow(flowId)` (and the backing `_flowTokens` dictionary) implement the "Window Flow Scoping (Grouped Flow Cancellation)" feature mentioned in the git log (`414250f feat: back button support and Window Flow Scoping...`), but:

- **It's undocumented.** Neither `ReactiveWindowSystem.md` nor `rMVVM.md` mentions flow tokens, scoping, or grouped cancellation anywhere — it's a public `IWindowService` API surface with zero usage guidance and zero internal callers (nothing in `Runtime/` calls `GetOrCreateFlowToken` itself; it's purely for external callers to opt into).
- **Nothing ever removes an entry except an explicit `CancelFlow` call.** If a consumer calls `GetOrCreateFlowToken("someFlow")` repeatedly (e.g. once per window open) but never calls `CancelFlow` for every flow ID it ever created (easy to miss — there's no "flow finished" lifecycle hook that does this automatically), the dictionary of `CancellationTokenSource` instances grows for the life of the service.
- **`WindowService.OnDestroy()` disposes `_stackChanged` but never disposes the `CancellationTokenSource`s left in `_flowTokens`** — on scene unload/service teardown, any un-cancelled `CancellationTokenSource` instances are simply dropped (not `Dispose()`d), which is a minor but real resource leak (CTS implements `IDisposable` specifically because of internal timer/registration state).

**Recommendation:** document the flow-token feature (even briefly) with an intended lifecycle example, and have `OnDestroy()` iterate `_flowTokens.Values` calling `Cancel(); Dispose();` the same way it already tears down `_stackChanged`.

---

## 8. Inconsistent `IModalViewModel` initialization pattern between the two shipped dialogs

- `AlertDialogViewModel._tcs` is a `readonly` field initialized inline: `new UniTaskCompletionSource<bool>()`. `ResultTask` is safe to read at any time.
- `ConfirmDialogViewModel._tcs` is **not** initialized until `Setup(...)` is called — it's `null` until then. If anything ever reads `ResultTask` before `Setup()` runs (easy to imagine with the `ShowModalAsync(config, modalViewModel, ...)` overload that takes a pre-built ViewModel, if a caller forgets to call `Setup` before handing it to the service, or resolves the VM a different way than the `setup` callback), it throws `NullReferenceException` instead of giving any diagnostic about "Setup was never called."

Both classes expose a `Setup(...)` method for configuring display text, but only `ConfirmDialogViewModel` also (re)creates the completion source inside `Setup` (presumably to support being shown more than once from a pooled/singleton VM — but nothing else in the package suggests ViewModels are pooled/reused; `WindowService` resolves/destroys per-show via VContainer + `Instantiate`/`Destroy`). This looks like copy-paste drift between the two dialogs rather than an intentional design choice.

**Recommendation:** make both consistent — either both initialize `_tcs` inline (simplest, matches "always resolved fresh" lifecycle) or both lazily create it in `Setup` with a documented guard (throw a clear exception from `ResultTask` if accessed pre-`Setup`).

---

## 9. Nice-to-have improvements (not bugs)

* **No shipped example of the actual "reactive" binding pattern.** The two Composers that ship in the package (`AlertDialogComposer`, `ConfirmDialogComposer`) only wire plain `UnityEngine.UI.Button.onClick` → ViewModel methods — neither uses an `R3.ReactiveProperty`/`Observable.Subscribe(...).AddTo(...)` binding, which is the entire premise of "Reactive MVVM" per `rMVVM.md` §3.3's own example (`_viewModel.HealthDisplay.Subscribe(...)`). A newcomer who only looks at the shipped code (as opposed to the prose docs) would have no concrete example of an R3-bound Composer. Consider shipping one trivial example (even a `ReactiveProperty<string>` message label) so the pattern is demonstrated in code, not just prose.
* **No `Samples~` folder.** UPM supports a `samples` entry in `package.json` pointing at a `Samples~` folder that Package Manager can let users import into their project (e.g. a demo scene with a `WindowService` wired up, the two dialogs, a sample `LifetimeScope`). Right now a new consumer has to piece together a working setup purely from markdown prose; an importable sample scene would de-risk onboarding a lot given how much manual wiring (`ContainerMapping`, `WindowConfig` assets, VContainer registration) this system needs before it does anything.
* **`WindowDatabase.GetConfigByName(string)`** does a `Configs.FirstOrDefault(...)` linear scan on every call and isn't cached like `GetConfig<T>()` is — minor, and currently unused by anything else in the package or docs (looks like dead/unexercised API).
* **Verbose `Debug.Log` calls left in `WindowDatabase.BuildCache`** (`Debug.Log("[WindowDatabase] Building cache...")`, one more per discovered config) will spam the console in any project with more than a few dialogs; consider gating behind a debug flag or dropping to the one-line summary.
* **`WindowBuilder<TViewModel>` doesn't actually use its own generic parameter to resolve anything** — `ExecuteAsync` just forwards `_prebuiltViewModel` (which can be `null` if `.WithViewModel(...)` was never called) to `ExecuteShowAsync`, which falls back to reflecting `config.Prefab`'s `WindowComposer<T>` exactly the same way the *non-generic* `WindowBuilder` does. The generic variant currently buys you compile-time type-checking on `.WithViewModel(vm)` only — worth a one-line doc comment clarifying that `TViewModel` doesn't drive resolution, so nobody assumes `Open<Foo>(config)` with no `.WithViewModel()` call behaves differently from the non-generic `Open(config)`.
* **`AnimatorWindowTransition.StateFinished`** polls `stateInfo.IsName(name) && normalizedTime >= 1f` right after calling `_animator.Play(name)` in the same method — if the Animator is already sitting in that exact state when `Play` is called again (e.g. a rapid re-show), `GetCurrentAnimatorStateInfo` can already report `IsName == true` with a stale `normalizedTime` from the *previous* play-through, letting `PlayShowAsync`/`PlayHideAsync` return instantly without the animation actually restarting. Minor edge case; worth a one-frame `await UniTask.Yield()` before the wait-loop if this matters for your use case.

---

## 10. Summary — priority order

| # | Finding | Severity |
|---|---|---|
| 2 | `ConfirmDialog.asset` points at `AlertDialog.prefab` — `ShowConfirmAsync` hangs forever | **High** — breaks a documented, headline feature out of the box |
| 6 | No re-entrancy guard on Composer show/hide transitions | Medium — visible glitches under rapid navigation |
| 7 | `_flowTokens` leak (unbounded growth, not disposed on teardown) | Medium — undocumented feature with a real resource leak |
| 3 | Global-namespace / `Game.UI.*` / `GameLib` leftovers from the monolith | Medium — collision risk + unprofessional surface for a public package |
| 1 | Docs describe a Model/VitalRouter/DataFlow layer this package doesn't ship | Medium — misleading onboarding |
| 8 | `ConfirmDialogViewModel` vs `AlertDialogViewModel` init inconsistency | Low — latent NRE footgun |
| 5 | `StackMode.Clear` XML doc contradicts actual (Replace-identical) behavior | Low — misleading IntelliSense |
| 4 | README dependency table miscounts asmdef references; package.json under-declares deps | Low — accuracy/polish |
| 9 | No R3-bound example Composer, no Samples~, minor dead/verbose code | Low — polish / onboarding friction |
