// TEST FIXTURE (not a real card). A NON-INTERACTIVE OnSecurityCheck WINDOW reactor: a battle-area Digimon that,
// when ITS OWNER's security is checked, draws 1 card — the observable, live-effect-model witness for the
// SecurityResolver OnSecurityCheck window (G3.5-W4). Shape modelled on the real EX5_053 [Security check] reactor
// (field-Digimon gate + GetCardFromHashtable checked-card predicate) with a plain DrawClass body (BT1_022 idiom)
// instead of an interactive play — so the window resolves inline through AutoProcessing.AutoProcessCheck with no
// agent choice. SCOPING is by the checked-card predicate: the reactor fires only when the checked card is owned by
// the reactor's own controller, so a reactor whose owner's security is NOT the one revealed stays dormant. No real
// card has the number "TfxOnSecurityCheckDraw", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnSecurityCheckDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnSecurityCheck)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1 on security check", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[When your security is checked] Draw 1. (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (!CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return false;
                }

                CardSource checkedCard = CardEffectCommons.GetCardFromHashtable(hashtable);
                // Scope to the reactor owner's OWN security being checked (the EX5_053 `securityCard.Owner ==
                // card.Owner` predicate). A reactor whose owner's security is not the revealed card stays dormant.
                return checkedCard != null && checkedCard.Owner == card.Owner;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
