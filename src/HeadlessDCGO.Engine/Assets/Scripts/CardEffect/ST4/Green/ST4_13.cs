// Source: Assets/Scripts/CardEffect/ST4/Green/ST4_13.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Ported per-card effect (Pierce + [Main] Digi-Burst 2 -> suspend 1 opponent Digimon).
//
// AS-IS region map:
//   OnDetermineDoSecurityCheck: Pierce (self)                     -> CardEffectFactory.PierceSelfEffect
//   OnDeclaration: [Main] <Digi-Burst 2> Suspend 1 opp Digimon    -> CardEffectFactory.DigiBurstEffect wrapping
//     an ActivatedSelectEffect (Mode.Tap) as the inner body, mirroring the AS-IS IDigiBurst wrapper
//     (new IDigiBurst(card.PermanentOfThisCard(), 2, activateClass).DigiBurst()) around the
//     SelectPermanentEffect coroutine (mode: Tap). Follows the existing Digi-Burst wiring pattern
//     (TfxDigiBurst: CardEffectFactory.DigiBurstEffect(card, count, innerEffect, description)) and the
//     existing select+Tap activated-select pattern (EX8_074's WhenDigivolving ActivatedSelectEffect(...,
//     SelectPermanentEffect.Mode.Tap, ...)). AS-IS ActivateClass.SetUpActivateClass(null, ActivateCoroutine,
//     -1, false, ...) -> canActivate=null, maxCountPerTurn=null (ORDER=-1), isOptional=false; the uniform
//     DigiBurstEffect/ActivatedSelectEffect primitives carry no separate isOptional/order fields (see
//     TfxDigiBurst), so no information is dropped by omitting them. AS-IS CanUseCondition's
//     IsExistOnBattleArea(card) + CanDigiBurst() gate is mirrored by the framework's own DigiBurst
//     resolution gate (ActivatedEffectResolver: TrashableDigivolutionCount(card, card.InstanceId) >= Count),
//     consistent with every other DigiBurstEffect usage in this codebase (no card adds an extra explicit
//     on-battle-area check alongside it).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class ST4_13 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnDeclaration)
        {
            // AS-IS CanSelectPermanentCondition: IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card).
            bool CanSelectPermanentCondition(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            // AS-IS: maxCount = Math.Min(1, MatchConditionPermanentCount(...)), canNoSelect: false,
            // canEndNotMax: false, mode: Tap. SelectPermanentEffect.BuildRequest already clamps maxCount to
            // the number of live matching candidates, so passing maxCount: 1 here is a faithful mirror (see
            // EX8_074's identically-shaped ActivatedSelectEffect(..., Mode.Tap, ...) usage).
            ICardEffect suspendOneOpponentDigimon = new ActivatedEffect(
                card, EffectTiming.None, canUse: null, canActivate: null,
                body: new ActivatedSelectEffect(card, CanSelectPermanentCondition, maxCount: 1, canNoSelect: false, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Tap, "Suspend 1 of your opponent's Digimon."),
                maxCountPerTurn: null, isOptional: false, "Suspend 1 of your opponent's Digimon.");

            cardEffects.Add(CardEffectFactory.DigiBurstEffect(
                card, count: 2, suspendOneOpponentDigimon,
                "[Main] <Digi-Burst 2> (Trash 2 of this Digimon's Digivolution cards to activate the effect below.) - Suspend 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
