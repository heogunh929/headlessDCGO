// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_102.cs — an Option.
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Main] branch; the
// [Security] branch already reuses [Main] via AddActivateMainOptionSecurityEffect (unchanged).
//   [Main] Trigger <Draw 1> (Draw 1 card from your deck) for every 2 security cards you have.
// AS-IS: ActivateClass on OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, CanActivateCondition = null,
//   ORDER=-1, ISOPTIONAL=false. ActivateCoroutine = new DrawClass(card.Owner, card.Owner.SecurityCards.Count / 2,
//   activateClass).Draw() — the draw count is read at activation time.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.SecurityCards` ->
//   `new Player(card.Context, card.Owner).SecurityCards`; DrawClass ctor -> mirror shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_102 : CEntity_Effect
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
                return "[Main] Trigger <Draw 1> (Draw 1 card from your deck) for every 2 security cards you have.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, new Player(card.Context, card.Owner).SecurityCards.Count / 2, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: $"Draw cards");
        }

        return cardEffects;
    }
}
