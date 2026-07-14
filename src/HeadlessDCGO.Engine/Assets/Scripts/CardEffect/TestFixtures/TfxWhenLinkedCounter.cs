// TEST FIXTURE (not a real card). An UNCAPPED [When Linked] host-self memory probe: whenever THIS Digimon (host)
// receives a link card, its owner gains 1 memory — no once-per-turn cap. Self-scoped to the HOST via the Digimon-self
// gate (CanTriggerWhenLinked with permanent == this card's own permanent, mirroring BT22_003 / BT22_035.IsEntermon).
// Uncapped so the observable is unmasked:
//   * one AddLinkCard fires the reactor exactly ONCE (+1) — WhenLinked is per-card (one subject = the host), no batch.
//   * linking TWO cards (two AddLinkCard calls) fires it TWICE (+2) — per-card, not a batch collapse.
//   * a permanent that did NOT receive the link does NOT gain (+0) — the host self-gate rejects it.
// Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): `MemoryBody(1)` ->
// `card.Owner.AddMemory(1, activateClass)`; CanTriggerWhenLinked Hashtable overload = (permanentCondition, sourceCondition).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWhenLinkedCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When this Digimon gets linked, gain 1 memory (uncapped).";

            // Host-self: this card's OWN permanent is the one that received the link (BT22_003 / BT22_035.IsEntermon).
            bool IsThisPermanent(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId == card.InstanceId;

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenLinked(hashtable, IsThisPermanent, null);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
