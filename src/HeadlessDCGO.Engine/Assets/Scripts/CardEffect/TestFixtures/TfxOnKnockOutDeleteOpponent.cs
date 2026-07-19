// TEST FIXTURE (not a real card). An OnKnockOut window reactor: a battle-area Digimon that, WHEN IT IS KNOCKED
// OUT (loses a battle), deletes all of its opponent's battle-area Digimon — the observable live witness that the
// BattleResolver OnKnockOut window (GetSkillInfos(OnKnockOut) -> PutStackedSkill, drained at the shared main-loop
// AutoProcessCheck alongside the OnDetermineDoSecurityCheck/Pierce window) actually fires a card-registered
// reactor. It replaces the invented EffectRegistry.Register(EffectBinding) probe in G3.5-D1 (B2). The delete is
// the SAME primitive real cards use in a coroutine (BT9_111/BT9_081: `new Player(...).Enemy!.GetBattleAreaDigimons()`
// -> `new DestroyPermanentsClass(list, CardEffectHashtable(activateClass)).Destroy()`) — no invented logic. The
// OnKnockOut window carries an empty hashtable (BattleResolver.ResolveKnockOutWindowAsync), so scoping is by live
// game state (the reactor owner's enemy), not a hashtable predicate. No real card is numbered
// "TfxOnKnockOutDeleteOpponent", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnKnockOutDeleteOpponent : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnKnockOut)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete opponent Digimon when knocked out", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[When this Digimon is knocked out] Delete all of your opponent's battle-area Digimon. (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool PermanentCondition(Permanent permanent) => true;

            bool CanUseCondition(Hashtable hashtable) => true;

            bool CanActivateCondition(Hashtable hashtable) =>
                new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition).Count > 0;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents =
                    new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition);
                await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
            }
        }

        return cardEffects;
    }
}
