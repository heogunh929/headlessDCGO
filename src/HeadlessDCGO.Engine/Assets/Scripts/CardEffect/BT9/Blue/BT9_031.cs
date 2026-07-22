// Source: DCGO/Assets/Scripts/CardEffect/BT9/Blue/BT9_031.cs — 1:1 headless mirror (W2-LevelEvoCost witness).
// MetalGarurumon (X Antibody) (BT9_031, Digimon / Blue). Three AS-IS timing blocks:
//   [None]           AddSelfDigivolutionRequirementStaticEffect — digivolve onto a [MetalGarurumon] top card for 1.
//   [When Digivolving] Unsuspend this Digimon + gain <Blocker> until the end of the opponent's turn.
//   [Your Turn][Once Per Turn] When this Digimon becomes unsuspended, if [MetalGarurumon] or [X Antibody] is among
//                    its digivolution cards, return all of the opponent's LOWEST-level Digimon to their owners' hands
//                    (the IsMinLevel witness — the level predicate GATES which enemy Digimon are bounced).
// ③ wiring: AS-IS GENUINELY stacks the digivolve window at EffectTiming.OnEnterFieldAnyone (DCGO
//   CardController.cs:1693 — `StackSkillInfos(effectHashtable, OnEnterFieldAnyone, CardEffectCondition)`; this is the
//   real, non-stale AS-IS key, and BT9_081 mirrors it verbatim under OnEnterFieldAnyone). The mirror instead keys
//   THIS card's [When Digivolving] under the dedicated EffectTiming.WhenDigivolving purely because the substrate
//   emits ONLY that key on a digivolve (DigivolveAction.cs:271; double-key registration forbidden — trigger-wiring
//   rule 3, the established mirror convention). Both spellings are correct — the CODE is right (the earlier note that
//   framed OnEnterFieldAnyone as "stale" was the wrong justification); the CanTriggerWhenDigivolving(hashtable, card)
//   gate body is kept verbatim.
// Substrate translations only: IEnumerator->async Task; `yield return ContinuousController.instance.StartCoroutine(X)`
//   -> `await X`; `card.PermanentOfThisCard()` (as a Permanent arg) -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
//   the unsuspend predicate `permanent == card.PermanentOfThisCard()` -> `permanent.InstanceId ==
//   card.PermanentOfThisCard().TopInstanceId` (BT2_002 idiom); `card.Owner.Enemy` -> `CardEffectCommons.OpponentOf(card)`
//   (HeadlessPlayerId, whose GetBattleAreaDigimons() extension + .Filter mirror AS-IS); `new HandBounceClaass(list,
//   CardEffectHashtable(activateClass)).Bounce()` -> `CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
//   list, activateClass, success:null, failure:null)` (the established HandBounceClaass mirror, BT15_082 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_031 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("MetalGarurumon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 1, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon and it gets Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Unsuspend this Digimon, and it gains <Blocker> until the end of your opponent's turn. (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();

                await CardEffectCommons.GainBlocker(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);
            }
        }

        if (timing == EffectTiming.OnUnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return opponent's all Digimon with the lowest level to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Bounce_BT9_031");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When this Digimon becomes unsuspended, if [MetalGarurumon] or [X Antibody] is in its digivolution cards, return all of your opponent's Digimon with the lowest level to their owners' hands.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMinLevel(permanent, CardEffectCommons.OpponentOf(card));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenPermanentUnsuspends(hashtable, (permanent) => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count((cardSource) => cardSource.CardNames.Contains("MetalGarurumon") || cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody")) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> boounceTargetPermanents = CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons().Filter(PermanentCondition);
                await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(targetPermanents: boounceTargetPermanents, activateClass: activateClass, successProcess: null, failureProcess: null);
            }
        }

        return cardEffects;
    }
}
