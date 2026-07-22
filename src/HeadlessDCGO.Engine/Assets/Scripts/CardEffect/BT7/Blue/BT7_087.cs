// Source: Assets/Scripts/CardEffect/BT7/Blue/BT7_087.cs
// Decision: STOP (coverage-exemplar tranche)
// Category: CardEffect
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Blue
//
// STOP EVIDENCE (retirement-guard / no-invention):
//   The [Main][Once Per Turn] "treat this Tamer as a level-5 blue Digimon and digivolve" block
//   (AS-IS BT7_087.cs :235,259) mutates `card.PermanentOfThisCard().IsPlaceToTrashDueToNotHavingDP`
//   ( = false before DigivolveIntoHandOrTrashCard, restored = true after ). The mirror
//   `Permanent.IsPlaceToTrashDueToNotHavingDP` (Permanent.cs:2827) is a GET-ONLY metadata-derived
//   property (default true; key GameFlowProcessor.PlaceToTrashDueToNoDpKey) with NO writer anywhere in
//   the engine. Porting this card 1:1 requires adding a WRITE-side surface (a setter stamping that
//   metadata key). The permitted single-surface exception for this tranche is explicitly READ-side only;
//   a write-side rule-bearing surface is "larger" -> STOP, do not invent.
//   The card's covered elements (ChangeCardColor/ChangePermanentLevel/DontHaveDP/TreatAsDigimon) are
//   already exercised by the ported exemplar BT17_026 (BT17/Blue), so the set-cover is not lost.
//
// (Comment-only stub retained -- no CEntity class emitted, build stays green.)
