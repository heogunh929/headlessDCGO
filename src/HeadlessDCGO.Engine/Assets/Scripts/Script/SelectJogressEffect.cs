// Source: Assets/Scripts/Script/SelectJogressEffect.cs
// Decision: PORT
// Category: AIUseful
// Priority: MEDIUM
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
// (SKEL-Exhaust) RECLASSIFIED: UI-flow orchestrator, NOT a standalone card-effect primitive. The AS-IS body is
// a MonoBehaviour that drives the jogress/DNA selection flow purely through Unity UI
// (GManager.instance.selectCardPanel.OpenSelectCardPanel; GetComponent<SelectPermanentEffect>();
// GetComponent<SelectDNACondition>(); StartCoroutine). The actual game decisions are the caller-supplied
// coroutine callbacks (endSelectCoroutine_Digivolve / _Jogress / _SelectDigivolutionRoots), and in headless
// those are driven by the ChoiceProvider-based DNA-digivolve special-play pipeline
// (CardEffectCommons.DNADigivolvePermanentsIntoHandOrTrashCard + SpecialPlayRecipeRegistry, Kind=DnaDigivolve).
// Its jogress-FRAME dependency also overlaps the census §4 field-frame slot-model gap. No engine-primitive
// body to port at this layer — left intentionally bodiless (superseded by the substrate DNA path).
