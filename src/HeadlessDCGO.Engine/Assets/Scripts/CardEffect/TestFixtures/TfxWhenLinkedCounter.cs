// TEST FIXTURE (not a real card). An UNCAPPED [When Linked] host-self memory probe: whenever THIS Digimon (host)
// receives a link card, its owner gains 1 memory — no once-per-turn cap. Self-scoped to the HOST via the Digimon-self
// gate (CanTriggerWhenLinked with permanent == this card's own permanent, mirroring BT22_003 / BT22_035.IsEntermon).
// Uncapped so the observable is unmasked:
//   * one AddLinkCard fires the reactor exactly ONCE (+1) — WhenLinked is per-card (one subject = the host), no batch.
//   * linking TWO cards (two AddLinkCard calls) fires it TWICE (+2) — per-card, not a batch collapse.
//   * a permanent that did NOT receive the link does NOT gain (+0) — the host self-gate rejects it.
// Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxWhenLinkedCounter : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.WhenLinked)
        {
            // Host-self: this card's OWN permanent is the one that received the link (BT22_003 / BT22_035.IsEntermon).
            bool IsThisPermanent(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId == card.InstanceId;

            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenLinked,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenLinked(ctx, card, IsThisPermanent, null),
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new MemoryBody(1),
                maxCountPerTurn: null,   // UNCAPPED — no cap to mask a per-event over/under-fire.
                isOptional: false,
                description: "[All Turns] When this Digimon gets linked, gain 1 memory (uncapped)."));
        }

        return effects;
    }
}
