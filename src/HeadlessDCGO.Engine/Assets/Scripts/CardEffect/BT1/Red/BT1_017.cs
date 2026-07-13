// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_017.cs
// 1:1 mirror of the original BT1_017 (BT1/Red).
//   [On Play] 1 of your Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card)
//             for the turn.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition),
//   CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card), ORDER=-1,
//   ISOPTIONAL=false, ActivateCoroutine = SelectPermanentEffect(Mode.Custom, maxCount = Min(1, count),
//   canNoSelect:false, canEndNotMax:false) -> per-selected ChangeDigimonSAttack(+1, UntilEachTurnEnd).
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit CanUse/CanActivate gates
//   (CanTriggerOnPlay / IsExistOnBattleArea / HasMatchConditionPermanent, not folded away) and
//   body=ActivatedTargetBuffEffect (AS-IS SelectPermanentEffect Mode.Custom + ChangeDigimonSAttack).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_017 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            const string description =
                "[On Play] 1 of your Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";

            // AS-IS CanSelectPermanentCondition: IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card).
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            // AS-IS CanUseCondition: CanTriggerOnPlay(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOnPlay(ctx, card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelect).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new ActivatedTargetBuffEffect(
                    card, CanSelect, maxCount: 1, ModifierHelpers.SecurityAttackDeltaKey, changeValue: 1,
                    EffectDuration.UntilEachTurnEnd,
                    "Select 1 Digimon that will get Security Attack +1."),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        return cardEffects;
    }
}
