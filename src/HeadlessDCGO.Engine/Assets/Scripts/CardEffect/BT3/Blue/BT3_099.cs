// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_099.cs — an Option.
//
// STOP (timing == EffectTiming.OptionSkill): "[Main] Neither player's Digimon can be deleted in battle for
// the turn." AS-IS: ActivateClass on OptionSkill, ActivateCoroutine =
// CardEffectCommons.GainCanNotBeDeletedPlayerEffect(permanentCondition = ANY battle-area permanent —
// IsPermanentExistsOnBattleArea, with NO owner filter, i.e. BOTH players' Digimon —
// canNotBeDestroyedByBattleCondition = self==AttackingPermanent || self==DefendingPermanent (only the two
// battlers in the current fight), effectDuration: UntilOwnerTurnEnd).
//
// The headless GainCanNotBeDeletedPlayerEffect (CardPortingFramework.cs:8787) -> GainToPlayerScope
// (CardPortingFramework.cs:8650) hardcodes ScopePlayerId = sourceCard.Owner and never sets ScopeAnyPlayer.
// The consumer path (BattleDeletionGate.PreventsBattleDeletion -> ContinuousScopeEvaluation ->
// PlayerScopeContinuousHelpers.CollectApplicable, PlayerScopeContinuousHelpers.cs:79-83) COARSE-FILTERS every
// candidate whose owner != ScopePlayerId BEFORE the permanentCondition/battle-predicate is evaluated. So this
// owner-scoped grant can only ever protect the CASTER'S OWN Digimon — the opponent's battler receives zero
// protection, contradicting the AS-IS "NEITHER player's Digimon can be deleted." The only escape hatch —
// ScopeAnyPlayerKey (PlayerScopeContinuousHelpers.cs:28: "the effect is NOT restricted to ScopePlayerId's
// permanents ... applies to EITHER player's cards") — is exposed by PlayerScopeModifierEffect but NOT by the
// GainCanNotBeDeletedPlayerEffect / GainToPlayerScope commons path (no scopeAnyPlayer parameter on either).
// Shipping the owner-scoped call would be a NARROWING of the printed effect (protects one side of every
// battle instead of both) — fidelity-over-coverage forbids an approximate/narrowed mapping. Making
// GainCanNotBeDeletedPlayerEffect accept a scopeAnyPlayer flag is an engine-file change, out of scope for a
// per-card porting pass. Per rule 4 this is a genuine primitive gap (a player-scope battle-deletion immunity
// that spans BOTH players). No cardEffects registered for OptionSkill. — 강모델
// if (timing == EffectTiming.OptionSkill) { ... }
//
//   [Security] Add this card to its owner's hand. -> AddThisCardToHandEffect (SecuritySkill), verbatim
//     (mirrors BT1_096/BT1_098/BT1_103/BT1_112/BT1_108/ST3_13/ST3_14). Unaffected by the [Main] gap and
//     fully ported below.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_099 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
