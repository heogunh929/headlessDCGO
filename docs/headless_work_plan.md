# Headless Work Plan

## Objective

Build the `HeadlessDCGO.Engine.Headless` layer into a deterministic .NET runtime that can eventually replace Unity scene/runtime responsibilities for AI/RL simulation, while preserving AS-IS source locations for card/rule logic.

## Current State

- `Headless/` runtime scaffold exists under `src/HeadlessDCGO.Engine`.
- Infrastructure v0 is implemented for coroutine runner, choice providers, no-op effect scheduler, random source, trace sink, logging, in-memory services, and `DcgoMatch` facade.
- The original `Assets/...` generated porting files are intentionally untouched.
- Local build verification is currently blocked by the machine having only .NET runtime, not .NET SDK.

## Milestones

1. `Headless` Infra v0
   - Provide deterministic no-op implementations for runtime, choices, effects, random, trace, and logging.
   - Keep Unity/Photon/TMPro/UI references out of `Headless/`.

2. In-Memory State Services
   - Add headless-only repositories and state movers for card definitions, legal actions, effects, and zones.
   - These services are not final game logic; they are testable substrate for later porting.

3. Choice And Action Harness
   - Build scripted and policy choice flows around `ChoiceRequest`/`ChoiceResult`.
   - Add action queue semantics compatible with AS-IS `MainPhaseAction` direction without referencing AS-IS types.

4. Effect Resolution Harness
   - Expand `EffectScheduler` from no-op resolution into deterministic queue processing.
   - Gradually replace AS-IS `Hashtable` effect context with `EffectContext`.

5. Runtime Step Loop
   - Evolve `DcgoMatch`/`HeadlessGameLoop` into reset/step/apply-action flow.
   - Add observation, legal action, terminal state, and result surfaces for RL usage.

6. AS-IS Logic Integration
   - Start with low-risk pure logic and zone movement.
   - Port card/rule/effect code only after Headless services can absorb Unity singleton, coroutine, and choice dependencies.

## Completed Progress

- Milestone 1 has a working no-op/deterministic `Headless` infrastructure baseline.
- Milestone 2 has in-memory card/effect/rule/zone services for early harness work.
- Milestone 5 has an initial `HeadlessGameLoop` connected to `DcgoMatch`, `EngineContext`, effect resolution, legal-action queries, runtime events, and trace output.
- Milestone 3 has an initial action queue so `ApplyActionAsync` queues `LegalAction` values and `StepAsync` consumes them.
- Milestone 5 exposes placeholder `ObservationSnapshot` and `ActionMask` values through `StepResult`.
- Milestone 3 now has an `IActionProcessor` hook so queued actions can call deterministic state transition handlers.
- Milestone 3 now has a metadata-driven action processor for low-risk headless actions: no-op/pass, terminal toggle, zone movement, shuffle, and effect enqueue.
- Milestone 3 now has typed action constants and factory helpers so callers do not need to hand-write metadata dictionaries.
- Milestone 3 now has typed action payload records for card movement, security movement, and effect enqueue actions.
- Milestone 5 now exposes typed player/zone/card-count observation snapshots instead of a generic metadata bag.
- Milestone 5 now has a deterministic observation encoder that converts typed snapshots into stable feature names and numeric vectors.
- Milestone 5 now has a deterministic action encoder that converts legal actions into stable action slots and mask vectors.
- Milestone 5 now resets queued actions, pending effects, pending tasks, rule state, zone state, last-action state, and step index across match initialization/reset.
- Milestone 5 now has a `HeadlessRlEnvironment` facade that returns encoded observation/action-mask step results for AI/RL loops.
- Milestone 5 now has terminal reward/discount fields and a configurable reward calculator based on `MatchResult`.
- Milestone 5 now lets metadata-driven terminal actions carry winner/draw/surrender/reason payloads into `MatchResult`.
- Milestone 5 now has a deterministic scenario runner that can execute scripted action sequences through `HeadlessRlEnvironment`.
- Milestone 5 now has reusable smoke scenarios for empty two-player setup, terminal win, terminal draw, and reward perspective configuration.
- Milestone 5 now has a scenario verifier that checks terminal state, step count, winner/draw/surrender result, reward, discount, and reason expectations.
- Milestone 5 now has a default smoke suite that runs the reusable scenarios and aggregates verification results.
- Milestone 5 now has a smoke suite reporter that creates pass/fail summaries and failure detail rows for CLI/test output.

## Active Next Target

Continue milestone 3 and milestone 5:

- Add executable tests that run `HeadlessSmokeSuite` and assert its report once a .NET SDK is available.
- Continue narrowing metadata-driven actions into final domain action records as AS-IS actions are ported.
- Start integrating real AS-IS zone/card state models behind `IZoneStateReader` and `IZoneMover`.
- Replace terminal-only reward calculation with real win/loss/scoring reward shaping once those rules are ported.
- Replace terminal metadata payloads with real AS-IS `EndGame`/`SetLose` result propagation once those flows are ported.

## Verification Gates

- `Headless/` contains no `UnityEngine`, `Photon`, `TMPro`, `UnityEngine.UI`, or `UnityEngine.EventSystems` references.
- `Headless/` contains no `NotImplementedException`.
- `HeadlessDCGO.Engine.csproj` remains unchanged unless a future step explicitly requires project-level changes.
- Once a .NET SDK is available, `dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj` must pass.
