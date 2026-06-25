# Headless Unity Dependency Progress

## Scope

- Source root inspected: `DCGO/Assets`
- This counts Unity-dependent original C# functions/methods, then compares them with current `src/HeadlessDCGO.Engine/Headless` replacement infrastructure.
- `DONE` means the Unity dependency category has a concrete Headless v0 replacement API that builds.
- `PARTIAL` means a Headless replacement exists, but does not fully cover all original Unity semantics.
- `OUT_OF_SCOPE` means visual/client-only Unity behavior is intentionally excluded from the headless runtime.

## Counts

- C# files inspected: 4765
- Methods parsed: 8452
- Unity-dependent methods documented: 8236
- In-scope replacement target: 7395
- DONE: 569
- PARTIAL: 6474
- PENDING: 352
- OUT_OF_SCOPE: 841

## Progress

- Strict completed progress: **569 / 7395** in-scope Unity-dependent functions
- Implemented or partially covered: **7043 / 7395** in-scope Unity-dependent functions
- Whole detected set, including out-of-scope UI/client functions: **569 / 8236** DONE

## Status Counts

| status | count |
|---|---:|
| DONE | 569 |
| OUT_OF_SCOPE | 841 |
| PARTIAL | 6474 |
| PENDING | 352 |

## Category Counts

| category | method count |
|---|---:|
| ChoiceInput | 4148 |
| ClassUnityBase | 2326 |
| Coroutine | 4803 |
| DebugTimePrefs | 1107 |
| GameObjectTransform | 4895 |
| NetworkClient | 3308 |
| OtherUnity | 352 |
| RenderingUI | 2756 |
| SceneLifecycle | 1878 |
| UnityAttribute | 50 |
| UnityDataLoading | 822 |

## Output

- Detailed function list: `docs/headless_unity_dependent_functions.csv`
