// Source: DCGO/Assets/Scripts/CardEffect/BT6/Black/BT6_106.cs (59 lines) — TRUE AS-IS 1:1 re-port.
// Witness card for the freshly-relocated IsMaxCost predicate (MinMax_DP_Cost_Level/Cost/IsMaxCost.cs).
//   [Main]     Delete all of your opponent's Digimon with the highest play cost (OptionSkill).
//   [Security] The same Main effect, custom description (SecuritySkill).
// Structure kept verbatim from the original inline `new ActivateClass()` + `DestroyPermanentsClass(...).Destroy()`
// (the BT9_111 sibling idiom); no invented helpers.
// Substrate translations only:
//   * IEnumerator -> async Task, `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`
//     (BT9_111 idiom).
//   * `card.Owner.Enemy.GetBattleAreaDigimons()` -> `new Player(card.Context, card.Owner).Enemy!
//     .GetBattleAreaDigimons()` (BT9_111 idiom).
//   * AS-IS `CardEffectCommons.IsMaxCost(permanent, card.Owner.Enemy, true)` (a `Player Enemy` owner arg) ->
//     the mirror overload `IsMaxCost(Permanent?, HeadlessPlayerId, bool)`; `card.Owner.Enemy` ->
//     `CardEffectCommons.OpponentOf(card)` (BT25_019 idiom). Predicate is evaluated, NOT flattened — the
//     `IsPermanentExistsOnOpponentBattleAreaDigimon` guard is kept verbatim ahead of it.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT6_106 : CEntity_Effect
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
                return "[Main] Delete all of your opponent's Digimon with the highest play cost.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (CardEffectCommons.IsMaxCost(permanent, CardEffectCommons.OpponentOf(card), true))
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

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition);
                await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Delete opponent's all Digimon with the highest play cost");
        }

        return cardEffects;
    }
}
