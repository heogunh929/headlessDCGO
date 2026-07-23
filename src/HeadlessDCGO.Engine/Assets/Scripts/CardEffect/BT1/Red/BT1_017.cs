// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_017.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_017 (BT1/Red).
//   [On Play] 1 of your Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card)
//             for the turn.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions. CanUseCondition/CanActivateCondition (CanTriggerOnPlay / IsExistOnBattleArea /
// HasMatchConditionPermanent) resolve via the existing bridge; the AS-IS `Func<Permanent,bool>`
// CanSelectPermanentCondition is expressed verbatim on the canonical Func<Permanent,bool> shape (id-flip 3b) —
// a single condition function serves both AS-IS call sites (HasMatchConditionPermanent/MatchConditionPermanentCount
// and SelectPermanentEffect.SetUp's canTargetCondition).
//
// UNRESOLVED MEMBERS (kept verbatim, not simplified/faked; see docs/audit/rebuild_p5_cards_missing.md): AS-IS
// `GManager.instance.GetComponent<SelectPermanentEffect>()` — the mirror `GManager` exposes no `GetComponent<T>()`
// at all; the mirror `SelectPermanentEffect` (Assets/Scripts/Script/SelectPermanentEffect.cs) exposes a
// DIFFERENT, simplified `SetUp` (no `canTargetCondition_ByPreSelecetedList`/`canEndSelectCondition`/
// `selectPermanentCoroutine`/`afterSelectPermanentCoroutine`/`cardEffect`) and no `Activate()` Task method — the
// actual resolution loop for this AS-IS pattern lives in the `ActivatedEffect`/`IEffectBody` resolver machinery,
// which the no-fallback rule forbids reaching for. None of the W1-W3 bridge batches touched this selection
// machinery. Kept in AS-IS shape (standard IEnumerator->Task / StartCoroutine->await translation only).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_017 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Security Attack +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] 1 of your Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";
            }

            // AS-IS `CanSelectPermanentCondition(Permanent permanent)` =
            // `IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)` — expressed over the id idiom (see
            // file header).
            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    // UNRESOLVED (see file header): AS-IS `GManager.instance.GetComponent<SelectPermanentEffect>()`
                    // / full AS-IS `SetUp(...)` / `.Activate()` — no mirror bridge exists. Kept verbatim.
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get Security Attack +1.", "The opponent is selecting 1 Digimon that will get Security Attack +1.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonSAttack(
                            targetPermanent: permanent,
                            changeValue: 1,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
