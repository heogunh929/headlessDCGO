# .NET Headless Source Classification Summary

Generated from the current `DCGO` directory after source refresh. C# files were inspected where readable. No production source files were modified.

## Counts

- USE: 8316
- PORT: 4258
- IGNORE: 67112
- REVIEW: 0
- Total classified paths: 79686

## Decision By Category

- IGNORE / Animation: 143
- IGNORE / AssetOnly: 36035
- IGNORE / EditorOnly: 20883
- IGNORE / Effect: 19
- IGNORE / Network: 434
- IGNORE / PlatformSDK: 8
- IGNORE / Scene: 11
- IGNORE / Sound: 52
- IGNORE / UI: 66
- IGNORE / Unknown: 9461
- PORT / AIUseful: 25
- PORT / BattleLogic: 4
- PORT / CardData: 1
- PORT / CardEffect: 4180
- PORT / CoreRule: 5
- PORT / DataLoader: 14
- PORT / GameState: 5
- PORT / UnityMixedLogic: 24
- USE / AIUseful: 3
- USE / CardData: 8310
- USE / CardEffect: 1
- USE / CoreRule: 1
- USE / DataLoader: 1

## First HIGH Priority Sources

- Card master data retained as `USE/HIGH`: 8310 Unity `.asset` records under `Assets/CardBaseEntity/**` and `Assets/Editor/Missing AAs/**`; convert these to JSON or typed .NET records before simulation.
- `Assets/Scripts/Script/TurnStateMachine.cs` was found and classified as `PORT/HIGH`; it contains phase progression, main-phase action handling, and end-game flow.
- High-priority C# source extraction list:
- `Assets/Scripts/Script/CEntity_Base.cs` - CardData; Remove UnityEngine dependency
- `Assets/Scripts/Script/GameRandom.cs` - CoreRule; Keep as-is
- `Assets/Scripts/Script/DeckData.cs` - CoreRule; Remove UnityEngine dependency
- `Assets/Scripts/Script/DeckCodeUtility.cs` - CoreRule; Remove UnityEngine dependency
- `Assets/Scripts/Script/DeckBuildingRule.cs` - CoreRule; Remove UnityEngine dependency
- `Assets/Scripts/Script/GameContext.cs` - GameState; Remove UnityEngine dependency
- `Assets/Scripts/Script/Player.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/Permanent.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardSource.cs` - GameState; Remove UnityEngine dependency
- `Assets/Scripts/Script/TurnStateMachine.cs` - CoreRule; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardController.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardObjectController.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/MainPhaseAction/MainPhaseAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/MainPhaseAction/PlayCardAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/MainPhaseAction/PassAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/MainPhaseAction/AttackPermanentAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/MainPhaseAction/ActivateCardAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/MainPhaseAction/ActivatePermanentAction.cs` - AIUseful; Remove Photon/Unity dependencies; bind to .NET turn state machine
- `Assets/Scripts/Script/AutoProcessing.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/AttackProcess.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/GManager.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/ICardEffect.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectInterfaces.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/SkillInfo.cs` - CardEffect; Keep as-is
- `Assets/Scripts/Script/MultipleSkills.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectCommons.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectFactory.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/PermanentEffectFactory.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Black/AD1_023.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_010.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_011.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_012.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_013.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_014.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_019.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_020.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_024.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Green/AD1_022.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Purple/AD1_018.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_001.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_002.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_003.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_004.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_005.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_006.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_007.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_008.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_009.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_025.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_015.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_016.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_017.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_021.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_003.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_004.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_029.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_030.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_031.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_033.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_034.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_035.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_036.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_039.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_040.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_041.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_043.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_044.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_086.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_096.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_097.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_098.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_099.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_100.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_101.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_115.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_007.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_008.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_066.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_067.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_068.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_070.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_073.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_074.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_076.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_077.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_078.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_079.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_081.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_082.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_083.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_088.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_089.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_108.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_109.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_110.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_111.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_112.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_113.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_001.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_002.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_010.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_011.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_012.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_015.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_016.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_017.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_018.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_021.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_022.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_023.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_025.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_026.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_085.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_090.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_091.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_092.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_093.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_094.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_095.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_114.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- ... and 4086 more; see `docs/dotnet_source_classification.csv` for the full list.

## Strong Unity Dependency But Core Logic

- `Assets/Scripts/Script/CEntity_Base.cs` - CardData; Remove UnityEngine dependency
- `Assets/Scripts/Script/DeckBuildingRule.cs` - CoreRule; Remove UnityEngine dependency
- `Assets/Scripts/Script/GameContext.cs` - GameState; Remove UnityEngine dependency
- `Assets/Scripts/Script/Player.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/Permanent.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardSource.cs` - GameState; Remove UnityEngine dependency
- `Assets/Scripts/Script/TurnStateMachine.cs` - CoreRule; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardController.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardObjectController.cs` - GameState; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/AutoProcessing.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/AttackProcess.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/GManager.cs` - BattleLogic; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/ICardEffect.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectInterfaces.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/MultipleSkills.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectCommons.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/CardEffectFactory.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/Script/PermanentEffectFactory.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Black/AD1_023.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_010.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_011.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_012.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_013.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_014.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_019.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_020.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Blue/AD1_024.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Green/AD1_022.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Purple/AD1_018.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_001.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_002.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_003.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_004.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_005.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_006.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_007.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_008.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_009.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Red/AD1_025.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_015.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_016.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_017.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/AD1/Yellow/AD1_021.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_003.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_029.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_030.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_035.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_036.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_039.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_040.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_041.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_043.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_044.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_086.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_096.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_097.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_098.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_099.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_100.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_101.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Blue/BT1_115.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_007.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_066.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_067.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_070.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_074.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_076.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_077.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_078.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_079.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_081.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_082.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_088.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_089.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_108.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_109.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_110.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_111.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_112.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Green/BT1_113.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_001.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_010.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_011.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_012.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_017.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_021.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_022.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_023.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_025.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_090.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_091.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_092.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_093.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_094.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_095.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Red/BT1_114.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/White/BT1_084.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_006.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_046.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_048.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_049.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_053.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_054.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_055.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_056.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_060.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_061.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_062.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_063.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_087.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_102.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_103.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_104.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_105.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_106.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT1/Yellow/BT1_107.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT10/Black/BT10_058.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT10/Black/BT10_059.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT10/Black/BT10_061.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- `Assets/Scripts/CardEffect/BT10/Black/BT10_066.cs` - CardEffect; Extract pure logic; Replace Coroutine with async/state machine
- ... and 3630 more; see `docs/dotnet_source_classification.csv` for the full list.

## REVIEW Files

- None

## Manual Review Resolution

- `Assets/Scripts/Script/FilterCardList.cs` moved from REVIEW to `PORT / DataLoader / MEDIUM` after function-level inspection.
- `Assets/Scripts/Script/GameplayOption.cs` moved from REVIEW to `PORT / AIUseful / MEDIUM` after function-level inspection.
- Remaining prior REVIEW files were moved to `IGNORE` because their functions are UI, modal, profile, text-link, button, drag flag, or RectTransform helpers only.

## Missing Or Risky References

- The refreshed tree includes `TurnStateMachine.cs`, removing the previous blocking missing-source issue.
- Many per-card effects depend on Coroutine-driven `IEnumerator` flows and singleton access through `GManager.instance` / `ContinuousController.instance`; the .NET port should introduce an explicit effect scheduler and state context first.
- `TurnStateMachine.cs` still has Photon/UI/Coroutine dependencies, so it should be extracted rather than copied directly.

## Recommended First Porting Order

1. Card master data: convert `Assets/CardBaseEntity/**/*.asset` and missing-AA card records to JSON or typed .NET records.
2. Static card/deck/random data types and loaders: `CEntity_Base.cs`, `GameRandom.cs`, `DeckData.cs`, `DeckCodeUtility.cs`, `DeckBuildingRule.cs`, `DataBase.cs`.
3. Headless game state model: `GameContext.cs`, `Player.cs`, `Permanent.cs`, `CardSource.cs`, zone collections, memory, security, trash, breeding, field.
4. Turn and phase runtime: extract `TurnStateMachine.cs` into a pure state machine with explicit inputs and no Photon/UI waits.
5. Deterministic command surface: `MainPhaseAction/*.cs`, `IGamePacket.cs`, `GamePacketFactory.cs`; replace Photon serialization where needed.
6. Rule/battle runtime: port `AutoProcessing.cs`, `AttackProcess.cs`, `CardController.cs`, `CardObjectController.cs` after the state model exists.
7. Effect runtime interfaces: `ICardEffect.cs`, `CardEffectInterfaces.cs`, `SkillInfo.cs`, `MultipleSkills.cs`, shared `CardEffectCommons` and `CardEffectFactory` helpers.
8. Legal action/target generation and data tools: port selection-condition classes plus `FilterCardList.cs` predicates and `GameplayOption.cs` simulation-relevant option flags.
9. Per-card effect batch: `Assets/Scripts/CardEffect/**/*.cs`, after the core Coroutine/effect scheduler is represented in .NET.

## Notes

- Card effect implementations are intentionally marked `PORT/HIGH`, not `IGNORE`, because they encode card-specific rules despite Coroutine, Photon, and Unity object usage.
- UI, animation, sound, scenes, prefabs, package cache, built client files, and Photon plugin code are marked `IGNORE` unless they are project-owned action/effect serialization helpers.
- `REVIEW` is now empty after function-level review of the remaining project scripts.
- `USE` means either pure C# utility or card/data assets that should be retained conceptually; Unity-authored `.asset` card data still needs conversion to a .NET-friendly format.
