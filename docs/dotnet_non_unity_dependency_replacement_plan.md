# .NET Non-Unity Dependency Replacement Plan

## Scope

This plan covers non-Unity dependencies found in `DCGO/Assets` that also matter when moving to `src/HeadlessDCGO.Engine/Headless`.

It does not ask to port `Assets/...` yet. The purpose is to define how Headless should avoid, replace, or preserve these dependencies when later code is connected.

## Summary

| action | dependency groups | meaning |
|---|---:|---|
| REMOVE_OR_REPLACE | 3 | Must not enter Headless core; replace with deterministic local abstractions if gameplay code needs the concept. |
| IGNORE_FOR_HEADLESS | 10 | Visual/client/input/chat dependency; do not implement in Headless unless a gameplay side effect is discovered later. |
| KEEP_IF_REQUIRED | 1 | Only keep as a normal .NET/NuGet dependency if a later selected library needs it. |
| REVIEW | 3 | Needs source-level review before final mapping. |

## High Priority

| dependency | reason | replacement |
|---|---|---|
| Photon | Largest dependency: 1,523 files, 4,274 occurrences. It represents network rooms, RPC, ownership, callbacks, and room/player state. | Replace with local deterministic match/session/action context: `DcgoMatch`, `HeadlessGameLoop`, action queue, `GameEvent`, `HeadlessPlayerId`, and future local session services. |
| SelectCardEffect | Likely project selection flow, not a third-party package. It may carry gameplay choice semantics. | Preserve the game choice meaning through `ChoiceRequest`, `ChoiceCandidate`, `ChoiceZone`, `ChoiceResult`, and `IChoiceProvider`. Remove UI object coupling. |
| Realtime | One unresolved symbol, likely `Photon.Realtime` or a domain alias. | Manually classify. If Photon, replace with local session state. If domain logic, map to a Headless service. |

## Dependency Decisions

| dependency | decision | replacement target |
|---|---|---|
| Photon | Replace | Local deterministic match/session/event APIs. No Photon transport, lobby, room, RPC, or network ownership in Headless. |
| WebSocketSharp | Remove | None for local RL. Future remote rollout transport should live outside Headless core and can use `System.Net.WebSockets`. |
| WindowsRuntimeApi | Replace if needed | Cross-platform BCL APIs such as `System.Net.Sockets`, `System.IO`, or `System.Net.WebSockets`, outside core unless needed. |
| TextMeshPro | Ignore | Plain string diagnostics via `ILogSink` / `EngineTrace` only. |
| Coffee.UIExtensions | Ignore | No Headless replacement. Visual effect only. |
| DOTween | Ignore | Use immediate state transitions or deterministic task/event sequencing when animation completion had gated logic. |
| Shapes2D | Ignore | No Headless replacement. Visual add-on only. |
| AutoLayout3D | Ignore | No Headless replacement. Layout/UI only. |
| Cinemachine | Ignore | No Headless replacement. Camera only. |
| WebGLInput | Ignore | Decisions enter through `IChoiceProvider`, policies, or actions. |
| ProfanityFilter | Ignore | Chat/social text filtering is outside AI/RL match simulation. |
| NetPyoung.WebP | Ignore | Headless loads card metadata, not card image codecs. |
| JetBrains.Annotations | Ignore | Use nullable reference types instead. |
| org.nuget.system.runtime.compilerservices.unsafe | Keep if required | Do not add now. Let NuGet bring it transitively if future .NET packages require it. |
| Unity NuGet | Review/remove | Unity scoped registry is not a Headless runtime dependency. Use normal NuGet packages only when needed. |
| Realtime | Review | Classify the single occurrence. Likely Photon replacement. |
| SelectCardEffect | Review/replace | Preserve gameplay choice semantics, replace UI implementation with Headless choice APIs. |

## Headless Replacement Locations

| concept | current Headless location |
|---|---|
| Network/session replacement | `Headless/Runtime`, `Headless/Bridge`, `Headless/Services` |
| UI/input choice replacement | `Headless/Choices`, `Headless/Runtime` |
| Animation sequencing replacement | `Headless/Coroutines`, `Headless/Runtime` |
| Text/log/debug replacement | `Headless/Services/ILogSink.cs`, `Headless/Diagnostics` |
| Data/image separation | `Headless/DataLoading` |

## Completion Criteria

- `Headless/` has no references to Photon, WebSocketSharp, DOTween, TMPro, Coffee UI extensions, WebGLInput, Cinemachine, or image/rendering add-ons.
- Gameplay choices originally routed through UI are represented as `ChoiceRequest`/`ChoiceResult`.
- Network-dependent gameplay flow receives explicit local match/player/action context instead of reading Photon globals.
- Data loaders do not require Unity package registries, image codecs, or visual assets.
- `HeadlessDCGO.Engine` continues to build with zero warnings and zero errors.

## Related Files

- `docs/dotnet_non_unity_dependency_summary.csv`
- `docs/dotnet_non_unity_dependency_details.csv`
- `docs/dotnet_non_unity_dependency_roles.csv`
- `docs/dotnet_non_unity_dependency_roles.md`
- `docs/dotnet_non_unity_dependency_replacement_plan.csv`
