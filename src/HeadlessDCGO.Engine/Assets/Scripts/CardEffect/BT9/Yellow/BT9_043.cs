// Source: DCGO/Assets/Scripts/CardEffect/BT9/Yellow/BT9_043.cs
// F4 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass), OnEndAttack SELF (top-card) timing
// only — an F1-Tier2-OnEndAttack witness card.
//
// Ported effect (AS-IS BT9_043.cs:84-136, timing OnEndAttack):
//   * [End of Attack][Once Per Turn] "You may add the top card of your security stack to your hand to unsuspend this
//     Digimon." AS-IS structure kept verbatim: inline `new ActivateClass()` with SetHashString("Unsuspend_BT9_043")
//     (:89) and SetUpActivateClass(..., 1, true, ...) = maxCountPerTurn 1 (ONCE PER TURN) + isOptional TRUE
//     ("You may"). CanUseCondition (:97-100) = CanTriggerOnAttack(hashtable, card). CanActivateCondition (:102-113)
//     = IsExistOnBattleArea(card) && Owner.SecurityCards.Count >= 1. ActivateCoroutine (:115-135): topCard =
//     SecurityCards[0]; AddHandCards({topCard}) THEN IReduceSecurity().ReduceSecurity() THEN, if still on the
//     battle area, IUnsuspendPermanents(self).Unsuspend().
//
// R6-P/F4 — RESOLVED (was RD-R6-03 sec->hand carrier STOP; superseded by this re-port). The mirror
// `CardObjectController.AddHandCards` now exists 1:1 (AS-IS RemoveFromAllArea withdraw + AddToHandAsync insert,
// deriving the OnAddHand window from the ->Hand CardMoved batch) and the mirror `IReduceSecurity`/
// `IUnsuspendPermanents` (CardController.cs, MIG3-3a/3b) are both live 1:1 primitives, so the AS-IS 3-call
// ActivateCoroutine sequence is portable literally instead of via the composite
// ReturnTopSecurityToHandThenUnsuspendSelfBody this card previously used. `IReduceSecurity`'s null-collector call
// is the AS-IS "zone-derived OnLoseSecurity, no manual emit" quirk kept verbatim (see CardController.cs's
// IReduceSecurity doc comment) — the Security->Hand zone move already performed by AddHandCards is what actually
// derives OnLoseSecurity, so the ReduceSecurity() call itself is an intentional AS-IS no-op placeholder, not
// dropped logic.
//
// NOTE (honest red, not a regression): the witness suite (tests/F1-Tier2-OnEndAttack.Tests) is RED on this
// worktree both BEFORE and AFTER this port, for an UNRELATED PRE-EXISTING reason: a
// `GManager.instance.turnStateMachine.gameContext`-null NRE inside
// `CEntity_EffectController.GetCardEffects` (CEntity_EffectController.cs:88), reached via
// `RaidAttackSwitch.HasRaid` -> `Permanent.HasRaid` -> `Permanent.EffectList` during the attack pipeline's raid
// check (AttackProcess.CounterTiming), fails ALL 10 tests in the suite — including the ones with no relation to
// this card. Confirmed identical failure count (10/10 failed) on a clean HEAD before this file was touched.
// Tracked as the "F1-OnEndAttack NRE-red" investigation item (memory: bigbang-v2-r1-r6-progress.md).
//
// Only the OnEndAttack self reactor is ported (same witness scoping as BT9_021 / the other F1 witnesses — only
// the timing under test is ported). The AS-IS timing==None self-digivolution-requirement static (:15-23) and the
// OnEnterFieldAnyone [When Digivolving] opponent-DP-down effect (:25-82) are orthogonal to the OnEndAttack self
// reactor and are deliberately OMITTED, unchanged from the prior port.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT9_043 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [End of Attack] add top security to hand to unsuspend this Digimon (timing OnEndAttack, SELF)
        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Add a security to hand to unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_BT9_043"); // AS-IS SetHashString("Unsuspend_BT9_043")
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack][Once Per Turn] You may add the top card of your security stack to your hand to unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.SecurityCount(card) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.SecurityCount(card) >= 1)
                {
                    CardSource topCard = new Player(card.Context, card.Owner).SecurityCards[0];

                    await CardObjectController.AddHandCards(new List<CardSource>() { topCard }, false, activateClass);

                    await new IReduceSecurity(card.Context, card.Owner, refCollector: null, activateClass).ReduceSecurity();

                    if (CardEffectCommons.IsExistOnBattleArea(card))
                    {
                        Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                        await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
