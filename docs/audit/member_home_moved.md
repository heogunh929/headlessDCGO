# 멤버 홈파일 이동 목록 (동일경로 위반, 기계측정 2026-07-28)

판정 기준: 양쪽 트리에 **단일 선언**으로 존재하는 멤버만(보수적). 오버로드 분산·부분클래스 다중선언 멤버는 제외됨 → 실제 수는 이보다 큼.

| 멤버 | AS-IS 홈파일 | 미러 홈파일 |
|---|---|---|
| `CanActivateSuspendCostEffect` | Assets/Scripts/Script/CardEffectCommons/CanUseEffects/CanSuspend.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `CanDeclareOptionDelayEffect` | Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OptionEffect.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `CanPlayAsNewPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `CanUnsuspend` | Assets/Scripts/Script/CardEffectCommons/CanUseEffects/CanUnsuspend.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `Create` | Assets/Scripts/Script/Networking/GamePacketFactory.cs | Assets/Scripts/Script/CEntity_EffectController.cs |
| `EnforceLocationCheck` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `GetUniqueColourCountOnOpponentsBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `GetUniqueColourCountOnOwnerBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOpponentsCardInTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOpponentsPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOwnersBreedingPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOwnersCardInTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOwnersHand` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOwnersPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionOwnersSecurity` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `HasMatchConditionPermanentDigivolutionCards` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistDigivolutionCards` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistInAnyTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistInSecurity` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistLinked` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBattleAreaActivate` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBattleAreaDigimon` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBattleAreaTrigger` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBreedingArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnBreedingAreaDigimon` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnExecutingArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnField` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnHand` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsExistOnTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsOpponentEffect` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsOpponentPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsOwnerEffect` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsOwnerPermanent` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnBattleAreaDigimon` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnBattleAreaTamer` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnBreedingArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnField` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOpponentBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOpponentBattleAreaDigimon` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOpponentBattleAreaTamer` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOwnerBattleArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOwnerBattleAreaDigimon` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOwnerBattleAreaTamer` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `IsPermanentExistsOnOwnerBreedingArea` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionOpponentsCardCountInTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionOpponentsPermanentCount` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionOwnersCardCountInHand` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionOwnersCardCountInTrash` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionOwnersPermanentCount` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons.cs |
| `MatchConditionPermanentCount` | Assets/Scripts/Script/CardEffectCommons/GameContextDeterminarion.cs | Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Save.cs |
| `isExistOnField` | Assets/Scripts/Script/CEntity_Effect.cs | Assets/Scripts/Script/CardEffectInterfaces.cs |
