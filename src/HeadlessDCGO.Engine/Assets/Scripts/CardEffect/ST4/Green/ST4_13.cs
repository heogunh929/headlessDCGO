// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_13.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_13 (ST4/Green).
//   [Security] Pierce.
//   [Main] <Digi-Burst 2> (Trash 2 of this Digimon's Digivolution cards to activate the effect below.) -
//          Suspend 1 of your opponent's Digimon.
// OnDetermineDoSecurityCheck: `CardEffectFactory.PierceSelfEffect(...)` is a GENUINE real AS-IS
// CardEffectFactory call (verified against DCGO CardEffectFactory/KeyWordEffects/Pierce.cs) — kept unchanged,
// calling the mirror's own same-named static factory per the task's exception rule.
// OnDeclaration: replaces the PREVIOUS pass's old-model `CardEffectFactory.DigiBurstEffect(...)` wrapping an
// `ActivatedSelectEffect` (both invented — AS-IS itself has NO `CardEffectFactory.DigiBurstEffect`; it inlines
// `new IDigiBurst(...)`) with the literal AS-IS inline `new ActivateClass()` + `new IDigiBurst(card
// .PermanentOfThisCard(), 2, activateClass).CanDigiBurst()`/`.DigiBurst()` + `GManager.instance
// .GetComponent<SelectPermanentEffect>()` Mode.Tap structure.
// UNRESOLVED (batch-3, kept verbatim): AS-IS `new IDigiBurst(Permanent, int, ICardEffect)` (CardController.cs:
// 2114) — the mirror has NO ported `IDigiBurst` class at all (a wholly undeclared type in this codebase; the
// existing `DigiBurstActivatedEffect`/`CardEffectFactory.DigiBurstEffect` is the invented replacement this pass
// retires, not a port of the real class). Kept verbatim per the no-invented-bridge rule; this necessarily adds
// new CS0246 lines for `IDigiBurst` beyond the 59-count baseline (reported to the caller).
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `Func<Permanent,bool> CanSelectPermanentCondition` -> the established
// `Func<HeadlessEntityId,bool>` id-shape idiom (IsOpponentBattleAreaDigimon(card, id)).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

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
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] <Digi-Burst 2> (Trash 2 of this Digimon's Digivolution cards to activate the effect below.) - Suspend 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    // UNRESOLVED (see file header): AS-IS `new IDigiBurst(Permanent, int, ICardEffect)` — no
                    // mirror port of this class exists. Kept verbatim.
                    if (new IDigiBurst(card.PermanentOfThisCard(), 2, activateClass).CanDigiBurst())
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                // UNRESOLVED (see file header): AS-IS `new IDigiBurst(Permanent, int, ICardEffect).DigiBurst()`
                // — no mirror port of this class exists. Kept verbatim.
                await new IDigiBurst(card.PermanentOfThisCard(), 2, activateClass).DigiBurst();

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        return cardEffects;
    }
}
