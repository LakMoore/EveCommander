# GitHub Copilot instructions for this repository

This repository primarily contains a .NET (C#) WPF bot framework and parsing library for the MMORPG Game Eve Online. Use the following guidance when making suggestions or generating code:

- Prefer idiomatic C# for .NET 9 and WPF XAML when editing UI files.
- Keep changes minimal and focused on the user-specified task.
- For new files, include appropriate namespaces matching existing projects (e.g., `Commander`, `BotLib`, `Sanderling`).
- Avoid modifying external dependencies unless the user requests it.

Common areas:
- UI: files under `Commander/` (XAML and code-behind)
- Plugins: `BotLibPlugins/` and `BotLib/`
- Parsing: `Sanderling/implement/eve-parse-ui/`

If unsure, prefer creating small, well-tested changes and run the project build locally.
