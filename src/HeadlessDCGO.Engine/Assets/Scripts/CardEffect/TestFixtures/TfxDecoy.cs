// TEST FIXTURE (not a real card — no real <Decoy> card is ported yet; this carries the REAL factory shape).
// [WhenPermanentWouldBeDeleted] returns the printed-keyword form CardEffectFactory.DecoySelfEffect
// (permanentCondition: null = the generic set — any of the owner's OTHER battle-area Digimon), exactly the
// AS-IS consumer shape (e.g. DCGO BT6_064.cs:42: DecoySelfEffect(isInheritedEffect:false, card, condition:null,
// permanentCondition, effectName, effectDiscription)). Used by the C-Del 3c-2b F68 window conversion to witness
// the retired-gate Decoy firing through the AS-IS PRE cut-in window.
//
// NOTE the AS-IS Decoy CanUse gates on IsByEffect(hashtable, enemy-owner) — a POSITIVE live-cardEffect read
// (design item RD-3C2B-02): the universal effect-delete sink threads NO live ICardEffect, so this fixture only
// fires when the deletion is driven through the faithful mirror DestroyPermanentsClass path (which threads the
// causing ActivateClass, CardController.cs:3435/3454-3469 mirror). Inert in actual play (no real card numbered
// "TfxDecoy").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDecoy : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            // AS-IS consumer permanentCondition shape (DCGO BT6_064.cs:18-35, minus the colour clause): another of
            // the owner's battle-area Digimon that is currently MARKED for deletion (willBeRemoveField).
            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        if (permanent.willBeRemoveField)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.DecoySelfEffect(
                isInheritedEffect: false, card: card, condition: null, permanentCondition: CanSelectPermanentCondition,
                effectName: "Decoy (Tfx)",
                effectDiscription: "<Decoy> (When one of your other Digimon would be deleted by an opponent's effect, you may delete this Digimon to prevent that deletion.) (test fixture)"));
        }

        return cardEffects;
    }
}
