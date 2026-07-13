// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_094.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_094 (BT1/Red) — an Option.
//   [Main]     Delete 1 of your opponent's Digimon with <Blocker>.
//   [Security] AddActivateMainOptionSecurityEffect (reuse the Main effect) — unchanged, already a genuine
//              CardEffectCommons.* bridge call (AS-IS name, AS-IS shape).
// AS-IS structure kept verbatim for [Main]: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass +
// local functions. CanUseCondition/CanActivateCondition resolve via the existing bridge; AS-IS
// `CanSelectPermanentCondition` (opponent battle-area Digimon + HasBlocker) uses the same
// Func&lt;HeadlessEntityId,bool&gt; + ContinuousKeywordGate idiom as BT1_023 (same predicate shape).
//
// UNRESOLVED MEMBERS (kept verbatim, not simplified/faked; see docs/audit/rebuild_p5_cards_missing.md):
//   - `card.BaseENGCardNameFromEntity` (AS-IS CardSource.cs:1359, `_cEntity_Base.CardName_ENG`) — the mirror
//     `CardSource` has no equivalent member (only `CardNames`/`EqualsCardName`/`ContainsCardName`, no single
//     "base ENG name" accessor). Used only as the informational `effectName` argument to `SetUpICardEffect`
//     (display/id string, not gameplay logic) — kept verbatim rather than substituted with a literal string.
//   - `GManager.instance.GetComponent<SelectPermanentEffect>()` / full AS-IS `SetUp(...)` / `.Activate()` — same
//     selection-machinery gap as BT1_017/BT1_023/BT1_092 (no mirror bridge; none of W1-W3 touched it).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_094 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Delete 1 of your opponent's Digimon with <Blocker>.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    return true;
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

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: $"Delete 1 Digimon with Blocker");
        }

        return cardEffects;
    }
}
