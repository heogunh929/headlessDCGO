# .NET Non-Unity Dependency Inventory

## Scope

- Source root inspected: `DCGO/Assets`
- Excluded from this report: `System.*`, `Microsoft.*`, `UnityEngine.*`, `UnityEditor.*`, and `com.unity.*` package names.
- Included: third-party packages, client SDKs, networking libraries, UI/text/tween plugins, manifest non-Unity packages, asmdef modules, and plugin DLLs.

## Summary

- Unique non-Unity dependency groups: 17
- Detailed dependency occurrences: 4937

## Action Counts By Dependency Group

| action | dependency groups |
|---|---:|
| IGNORE_FOR_HEADLESS | 10 |
| KEEP_IF_REQUIRED | 1 |
| REMOVE_OR_REPLACE | 3 |
| REVIEW | 3 |

## Action Counts By Occurrence

| action | occurrences |
|---|---:|
| IGNORE_FOR_HEADLESS | 635 |
| KEEP_IF_REQUIRED | 1 |
| REMOVE_OR_REPLACE | 4298 |
| REVIEW | 3 |

## Category Counts

| category | dependency groups |
|---|---:|
| Animation | 1 |
| Annotation | 1 |
| Camera | 1 |
| ClientTextFiltering | 1 |
| ImageCodec | 1 |
| Network | 2 |
| NuGetRuntime | 1 |
| PackageRegistry | 1 |
| PlatformAPI | 1 |
| PlatformInput | 1 |
| UI | 1 |
| UIEffect | 1 |
| UnknownNonSystem | 2 |
| VisualAddOn | 2 |

## Top Dependencies By File Count

| dependency | action | files | occurrences | categories |
|---|---|---:|---:|---|
| Photon | REMOVE_OR_REPLACE | 1523 | 4274 | Network |
| TextMeshPro | IGNORE_FOR_HEADLESS | 58 | 109 | UI |
| Coffee.UIExtensions | IGNORE_FOR_HEADLESS | 37 | 104 | UIEffect |
| DOTween | IGNORE_FOR_HEADLESS | 32 | 278 | Animation |
| Shapes2D | IGNORE_FOR_HEADLESS | 15 | 82 | VisualAddOn |
| AutoLayout3D | IGNORE_FOR_HEADLESS | 8 | 9 | VisualAddOn |
| ProfanityFilter | IGNORE_FOR_HEADLESS | 7 | 15 | ClientTextFiltering |
| WebSocketSharp | REMOVE_OR_REPLACE | 7 | 16 | Network |
| WebGLInput | IGNORE_FOR_HEADLESS | 5 | 24 | PlatformInput |
| Cinemachine | IGNORE_FOR_HEADLESS | 2 | 4 | Camera |
| NetPyoung.WebP | IGNORE_FOR_HEADLESS | 2 | 8 | ImageCodec |
| JetBrains.Annotations | IGNORE_FOR_HEADLESS | 1 | 2 | Annotation |
| Realtime | REVIEW | 1 | 1 | UnknownNonSystem |
| SelectCardEffect | REVIEW | 1 | 1 | UnknownNonSystem |
| Unity NuGet | REVIEW | 1 | 1 | PackageRegistry |
| WindowsRuntimeApi | REMOVE_OR_REPLACE | 1 | 8 | PlatformAPI |
| org.nuget.system.runtime.compilerservices.unsafe | KEEP_IF_REQUIRED | 1 | 1 | NuGetRuntime |

## Output

- Summary CSV: `docs/dotnet_non_unity_dependency_summary.csv`
- Detail CSV: `docs/dotnet_non_unity_dependency_details.csv`
