# Properties and Events Search Bar

A Visual Studio extension that adds a live search bar to the top of the **Properties** window, so you can quickly filter down long property and event lists instead of scrolling through them.

## Features

- **Instant filtering**: type to filter properties (or events, in the Events view) by name, display name, or category, updating live as you type.
- **Works with nested/expandable properties**: searching a name buried inside an expandable property (like `Font` or `Colors`) still surfaces the parent row, at any nesting depth.
- **Theme-aware**: the search bar matches your current Visual Studio color theme automatically, including live theme switches.
- **Non-intrusive**: when the search box is empty, the Properties window behaves exactly as stock Visual Studio does.

## Requirements

- Visual Studio 2022 or 2026 (Community, Professional, or Enterprise)
- WinForms designer (the Properties window's `PropertyGrid`)

## Installation

1. Download `vs-properties-and-events-search-bar.vsix` from the [Releases](../../releases) page, or install directly from the Visual Studio Marketplace.
2. Double-click the `.vsix` file, or install it via **Extensions → Manage Extensions** in Visual Studio.
3. Restart Visual Studio when prompted.

## Usage

Open the Properties window on any WinForms component and start typing in the search bar at the top. The grid filters live as you type; press `Esc` to clear the query and see everything again.

## Building from source

```
msbuild vs-properties-and-events-search-bar.sln /p:Configuration=Release
```

The built `.vsix` will be in `vs-properties-and-events-search-bar\bin\Release\`.

## Project structure

| File | Purpose |
|---|---|
| `PropertiesSearchPackage.cs` | VSPackage entry point |
| `PropertiesWindowWatcher.cs` | Detects when the Properties window opens and mounts the search bar |
| `PropertyGridLocator.cs` | Locates the shell's live `PropertyGrid` control |
| `SearchBarInjector.cs` | Creates and positions the themed search bar UI |
| `FilterController.cs` | Owns filter state and swaps filtered proxies into the grid's selection |
| `FilteredComponent.cs` | `ICustomTypeDescriptor` proxy that filters properties/events by query, including nested properties |
| `DiagLog.cs` | Diagnostic logging |
