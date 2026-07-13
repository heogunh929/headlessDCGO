// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_092.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_092 (BT1/Red) — an Option.
//   [Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn.
// AS-IS structure kept verbatim: ONE ActivateClass, CanUseCondition = CanTriggerOptionMainEffect,
// CanActivateCondition = null (unconditional; AS-IS passes null literally, not folded away), ActivateCoroutine
// runs `new DrawClass(card.Owner, 2, activateClass).Draw()` THEN (guarded INSIDE the coroutine by
// HasMatchConditionPermanent, not a separate CanActivateCondition) the buff-select. `new DrawClass(...)` now
// resolves via the mirror `DrawClass` ctor (Assets/Scripts/Script/CardController.cs) — substrate shape needs an
// explicit `EngineContext` (AS-IS `Player` implicitly carries it) and the cause as a `HeadlessEntityId?` (AS-IS
// passes the `ICardEffect` directly) — translated as `card.Context` / `activateClass.EffectSourceCard?.InstanceId`,
// the same "ICardEffect -> its EffectSourceCard's id" idiom the task brief's own CanNotBeAffected example uses.
//
// UNRESOLVED MEMBERS (kept verbatim, not simplified/faked; see docs/audit/rebuild_p5_cards_missing.md):
//   - `card.BaseENGCardNameFromEntity` (AS-IS CardSource.cs:1359) — no mirror equivalent (informational
//     `effectName` argument only, not gameplay logic); same gap as BT1_094.
//   - `GManager.instance.GetComponent<SelectPermanentEffect>()` / full AS-IS `SetUp(...)` / `.Activate()` — same
//     gap as BT1_017/BT1_023 (no mirror bridge for this selection machinery). Kept in AS-IS shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Trigger <Draw 2> (Draw 2 cards from your deck). Then 1 of your Digimon gets +2000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 2, activateClass.EffectSourceCard?.InstanceId).Draw();

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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP +2000.", "The opponent is selecting 1 Digimon that will get DP +2000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: permanent,
                            changeValue: 2000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
