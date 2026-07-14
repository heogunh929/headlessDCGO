// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_023.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_023 (BT1/Red) — a Digimon.
//   [On Play] Delete 1 of your opponent's Digimon with <Blocker>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions. CanUseCondition/CanActivateCondition resolve via the existing bridge; AS-IS
// `CanSelectPermanentCondition(Permanent permanent)` = `IsPermanentExistsOnOpponentBattleAreaDigimon(permanent,
// card) && permanent.HasBlocker` is expressed over the established `Func<HeadlessEntityId,bool>` idiom (mirror
// (R1-c) rehoused: `permanent.HasBlocker` is now the AS-IS getter `new Permanent(context, id).HasBlocker`).
//
// UNRESOLVED MEMBERS (kept verbatim, not simplified/faked; see docs/audit/rebuild_p5_cards_missing.md): AS-IS
// `GManager.instance.GetComponent<SelectPermanentEffect>()` / full AS-IS `SetUp(...)` / `.Activate()` — same gap
// as BT1_017 (no mirror bridge for this selection machinery; none of W1-W3 touched it). Kept in AS-IS shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_023 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Delete 1 of your opponent's Digimon with <Blocker>.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (new Permanent(card.Context, id).HasBlocker)
                    {
                        return true;
                    }
                }

                return false;
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
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        return cardEffects;
    }
}
