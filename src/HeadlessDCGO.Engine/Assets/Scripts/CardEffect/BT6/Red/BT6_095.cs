// Source: DCGO/Assets/Scripts/CardEffect/BT6/Red/BT6_095.cs (69 lines) — TRUE AS-IS 1:1 re-port.
// Witness card for the freshly-relocated IsMinDP predicate (MinMax_DP_Cost_Level/DP/IsMinDP.cs).
//   [None]     Ignore this card's color requirements while the owner has a [Three Musketeers] Digimon on the
//              battle area (IgnoreColorConditionClass).
//   [Main]     Delete all of your opponent's Digimon with the lowest DP (OptionSkill).
//   [Security] The same Main effect, custom description (SecuritySkill).
// Structure kept verbatim (inline kind-classes + `DestroyPermanentsClass(...).Destroy()`, the BT9_109/BT9_111
// sibling idioms); no invented helpers.
// Substrate translations only:
//   * IEnumerator -> async Task, `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`.
//   * `card.Owner.Enemy.GetBattleAreaDigimons()` -> `new Player(card.Context, card.Owner).Enemy!
//     .GetBattleAreaDigimons()` (BT9_111 idiom).
//   * AS-IS `IsMinDP(permanent, card.Owner.Enemy)` (`Player Enemy` owner) -> mirror overload `IsMinDP(Permanent?,
//     HeadlessPlayerId, ...)`; `card.Owner.Enemy` -> `CardEffectCommons.OpponentOf(card)` (EX7_014 idiom).
//     Predicate is evaluated, NOT flattened.
//   * AS-IS `HasMatchConditionOwnersPermanent(card, (permanent) => ...)` takes a `Func<Permanent,bool>` in the
//     mirror too, so the VERBATIM trait predicate is passed directly (no adapter needed). The AS-IS operator
//     precedence (`&& ... || ...`) is preserved exactly.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Red;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT6_095 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.IsDigimon && permanent.TopCard.CardTraits.Contains("Three Musketeers") || permanent.TopCard.CardTraits.Contains("ThreeMusketeers"));
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Delete all of your opponent's Digimon with the lowest DP.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMinDP(permanent, CardEffectCommons.OpponentOf(card));
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
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Delete opponent's all Digimon with the lowest DP");
        }

        return cardEffects;
    }
}
