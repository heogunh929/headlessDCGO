// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_057.cs — a Digimon (two timing blocks).
// 1:1 mirror of the original BT3_057.
//   [When Digivolving] Suspend 1 of your opponent's Digimon. It doesn't unsuspend during your opponent's
//   next unsuspend phase.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone gated by CanTriggerWhenDigivolving (ported under
//   the WhenDigivolving branch — BT1_074/ST1_08/BT1_017/BT1_084 idiom: the bridge routes OnEnterFieldAnyone
//   activated selects nowhere live). CanActivateCondition = IsExistOnBattleArea(card) &&
//   HasMatchConditionPermanent(opponent battle-area Digimon). ORDER=-1, ISOPTIONAL=false. ActivateCoroutine:
//   SelectPermanentEffect(Mode.Tap, maxCount=Min(1,count), canNoSelect:false, canEndNotMax:false) — mandatory
//   pick of 1 opponent Digimon, suspended by the Tap mode itself — THEN per selected permanent,
//   CardEffectCommons.GainCantUnsuspendNextActivePhase.
//
//   (timing None) 1 of your Digimon gets <Security Attack +1> for the turn while it is your turn and at
//   least 1 of your opponent's Digimon is suspended.
//   AS-IS: CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue:1, isInheritedEffect:false, card,
//   condition: IsExistOnBattleArea && IsOwnerTurn && HasMatchConditionOpponentsPermanent(p => p.IsDigimon &&
//   p.IsSuspended)) — verbatim factory match (the opponent-scope helper already restricts to battle-area
//   Digimon, folding the redundant IsDigimon check).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Tap) for the [When Digivolving] branch (the
// BT1_082 Mode.Tap idiom — suspend IS the select mutation, no separate cost step), with onEachSelected wiring
// the AS-IS SelectPermanentCoroutine follow-up (GainCantUnsuspendNextActivePhase).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_057 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: null,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Tap,
                    description: "[When Digivolving] Suspend 1 of your opponent's Digimon. It doesn't unsuspend during your opponent's next unsuspend phase.",
                    onEachSelected: id => CardEffectCommons.GainCantUnsuspendNextActivePhase(
                        new Permanent(card.Context, id, card.Owner), card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Suspend 1 of your opponent's Digimon. It doesn't unsuspend during your opponent's next unsuspend phase."));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => CardEffectCommons.IsSuspended(card, id));

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
