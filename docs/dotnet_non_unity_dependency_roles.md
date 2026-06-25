# Non-Unity Dependency Roles

This document explains what each non-Unity dependency appears to do in the original Unity project, and what that means for the .NET Headless engine.

## Network And Session

| dependency | original role | Headless meaning |
|---|---|---|
| Photon | Online multiplayer SDK: rooms, joining, player sync, RPC, events, ownership, room/player properties, network callbacks. | Replace with deterministic local match/session/action/event context. Do not port Photon transport into Headless core. |
| WebSocketSharp | WebSocket transport library used by or near Photon. | Remove for local AI/RL. Future remote rollout transport should be outside core Headless. |
| WindowsRuntimeApi | Windows/UWP-specific networking/storage APIs. | Avoid in cross-platform Headless core; replace retained behavior with standard .NET APIs only if needed. |
| Realtime | Unresolved dependency name, likely `Photon.Realtime` or a domain/session alias. | Manual review. If Photon-related, map to local session/player state. |

## UI, Input, Visuals

| dependency | original role | Headless meaning |
|---|---|---|
| TextMeshPro | UI text rendering: labels, card text display, buttons, popups, logs, input fields. | Remove. Keep only plain strings in logs/traces/data. |
| Coffee.UIExtensions | UGUI effects such as particles, UIEffect, Unmask. | Visual-only; remove. |
| DOTween | Animation/tween sequencing for card movement, UI transitions, and effect presentation. | Remove animation. If logic depended on animation completion, replace with deterministic task/step sequencing. |
| Shapes2D | 2D shape rendering for UI decorations, gauges, or graphics. | Visual-only; remove. |
| AutoLayout3D | 3D object/card placement helper in scene space. | Replace gameplay-relevant ordering with pure zone/order state; otherwise remove. |
| Cinemachine | Camera movement, zoom, framing, and scene presentation. | Camera-only; remove. |
| WebGLInput | Browser/WebGL input and IME helper. | Replace human input with actions or `IChoiceProvider`. |
| NetPyoung.WebP | WebP image loading/decoding for card or UI images. | Remove; Headless loads metadata/rules, not images. |

## Client And Tooling

| dependency | original role | Headless meaning |
|---|---|---|
| ProfanityFilter | Chat/name/free-text profanity filtering. | Outside local AI/RL simulation; remove. |
| JetBrains.Annotations | IDE/static-analysis annotations such as nullable hints. | Remove or replace with C# nullable reference types. |
| org.nuget.system.runtime.compilerservices.unsafe | Low-level runtime helper, often transitive for other packages. | Do not add directly. Keep only if future .NET dependencies require it. |
| Unity NuGet | Unity package registry configuration. | Not a runtime dependency. Use normal NuGet references in .NET only when needed. |

## Project-Specific Dependency

| dependency | original role | Headless meaning |
|---|---|---|
| SelectCardEffect | Project selection/effect flow, likely where gameplay choices are routed through UI. | Do not ignore. Preserve gameplay selection semantics with `ChoiceRequest`, `ChoiceCandidate`, `ChoiceZone`, `ChoiceResult`, and `IChoiceProvider`; remove UI object coupling. |

## Practical Rule

For Headless work:

- Network dependencies become local deterministic session/event/action context.
- UI/input dependencies become `IChoiceProvider` or direct action selection.
- Animation dependencies become immediate state transitions or deterministic task sequencing.
- Visual/image/camera dependencies are ignored.
- Project-specific choice/effect flows are reviewed carefully because they may contain gameplay meaning.

Related files:

- `docs/dotnet_non_unity_dependency_summary.csv`
- `docs/dotnet_non_unity_dependency_details.csv`
- `docs/dotnet_non_unity_dependency_replacement_plan.csv`
- `docs/dotnet_non_unity_dependency_replacement_plan.md`
