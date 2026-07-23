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
// RESOLVED (bridge W5, was the batch-3 UNRESOLVED finding): AS-IS `new IDigiBurst(Permanent, int, ICardEffect)`
// (CardController.cs:2114) is now the mirror `IDigiBurst` class (Script/CardController.cs region "Digi-Burst",
// docs/audit/rebuild_bridge_w5_notes.md) at the AS-IS ctor shape.
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `card.PermanentOfThisCard()` (used as a `Permanent`) -> `ICardEffect.ResolvePermanentOfThisCard
// (card)` (the BT1_001 convention); the AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` is kept
// Permanent-shaped as the local `PermanentCondition(Permanent)` fed directly to
// HasMatchConditionPermanent/MatchConditionPermanentCount AND SelectPermanentEffect.SetUp's canTargetCondition
// (id-flip 3b canonical overload — no id-shape sibling needed).
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

            bool PermanentCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).CanDigiBurst())
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).DigiBurst();

                if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, PermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: PermanentCondition,
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
