// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_106.cs
// AS-IS has two branches (an Option):
//   [Main] All of your Digimon with <Blocker> or <Reboot> gain <Security Attack +1> for the turn.
//   [Security] Add this card to its owner's hand.  -> AddThisCardToHandEffect (verbatim shape, mirrors
//     ST3_14).
//
// STOP [Main] (genuine primitive gap, grepped 2x+ per rule 4): needs a no-select, activated, duration-tagged
// ("for the turn") player-scope Security-Attack buff scoped to only the owner's Digimon that HAVE Blocker OR
// Reboot (an arbitrary per-permanent predicate). Grepped Assets/Scripts/Script/CardEffectCommons/
// CardPortingFramework.cs: the one existing duration-aware activated player-scope buff primitive,
// ActivatedPlayerScopeBuffEffect (backing CardEffectFactory.PlayerScopeBuffSAttackEffect /
// PlayerScopeBuffDpEffect / PlayerScopeBuffSecurityDpEffect / OpponentScopeBuffSAttackEffect), only accepts
// `scopeCardType` (a coarse CardType-equality filter, e.g. "Digimon") and `scopeZone` — it has NO
// `scopePredicate` parameter, unlike its always-on CONTINUOUS twin (PlayerScopeModifierEffect, which does
// carry a `Func<CardSource,bool> scopePredicate` via CardEffectFactory.ScopePred — used by this set's
// BT3_075 restriction, but that is the wrong primitive family: continuous effects hardcode
// `duration: null`/permanent, not "for the turn"). CardEffectCommons.ChangeDigimonSAttackPlayerEffect DOES
// accept a `Func<Permanent,bool> permanentCondition` + `EffectDuration` (the exact AS-IS shape), but it is
// an imperative "mutate the registry now" helper with zero existing callers in any ported card — no
// composed IActivatedCardEffect invokes it from the OptionSkill activation flow
// (ActivatedEffectResolver.ResolveListAsync's switch has no matching case). No factory composes "no-select,
// predicate-filtered, duration-tagged, activated player-scope buff." Per rule 4 this is a primitive gap,
// out of scope for a single-card porting pass. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_106 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "All of your Digimon with <Blocker> or <Reboot> gain <Security Attack +1> for the
        // turn." — needs a no-select, predicate-filtered, duration-tagged, activated player-scope buff
        // primitive that does not exist yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
