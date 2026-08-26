# rMVVM

![rMVVMLogo](Documentation/rMVVMLogo.jpg)

> A lightweight **Reactive MVVM** architecture for Unity, paired with **rWS (Reactive Window System)** for awaitable, DI-driven UI navigation.

## Features

- ⚡ Reactive UI with R3
- 🧩 VContainer dependency injection
- 🪟 Awaitable window routing (rWS)
- 🧠 Clean MVVM architecture
- 🧪 Testable, framework-agnostic ViewModels
- 🚀 Minimal boilerplate

## Documentation

Start here based on what you need to understand:

* **[rMVVM.md](Documentation/rMVVM.md)** — Core architecture: the Model/ViewModel/Composer/View layers, DI patterns, and UI assembly strategies. Start here if you're new to the framework.
* **[ReactiveWindowSystem.md](Documentation/ReactiveWindowSystem.md)** — UI navigation routing: how windows are opened, stacked, and composed. The reactive window stack and its consumer patterns (e.g., InputConfig). For detailed case studies like input arbitration that depends on observable window state.
* **[DataFlow.md](Documentation/DataFlow.md)** — Data initialization and persistence: how game state enters the app (ScriptableObjects + loaded saves), flows reactively at runtime, and is saved back to disk via DTOs.

All three are interconnected via cross-references and share a consistent vocabulary. Pick one based on your immediate question, and follow the links for deeper context.
