// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs
// (RD-3A-01 resolved) 1:1 mirror of the AS-IS StartOfMainAttack grant. The RD-3A-01 STOP was retired: the
// OnStartMainPhase auto-fire window that this grant needs ALREADY EXISTS in the mirror and is fully wired —
//   TurnStateMachine.MainPhaseAsync:428  await autoProcessing.StackSkillInfos(null, EffectTiming.OnStartMainPhase)
//     -> AutoProcessing.GetSkillInfos:932 (per field permanent scan :960)  permanent.EffectList(timing)
//        -> Permanent.EffectList_ForCard  foreach EffectList_Added(timing)
//           -> Permanent.EffectList_Added:1920  walks UntilOwnerTurnEndEffects and invokes GetCardEffect(timing),
//              which yields THIS grant's ActivateClass only at EffectTiming.OnStartMainPhase.
// So the grant surfaces at the owner's next main-phase entry and its mandatory SelectAttackEffect offer fires.
// The commons STOP wrapper (CardEffectCommons.cs) is removed; this home file is the sole surviving copy.
// Substrate translation (verbatim logic): coroutine IEnumerator -> Task; the AS-IS tail
// `yield return CreateDebuffEffect(targetPermanent)` is the UI debuff animation (UI-only, stripped — as in the
// sibling ChangeLinkMax/CanNotBeDeletedByBattle grants); `GManager.instance.turnStateMachine.gameContext.TurnPlayer
// == targetPermanent.TopCard.Owner` -> IsOwnerTurn(targetPermanent.TopCard); `targetPermanent.TopCard.Owner`
// (AS-IS Player) -> new Player(context, ownerId); ActivateCoroutine1's StartCoroutine(Activate()) -> await Activate().
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public static partial class CardEffectCommons
{
    /// <summary>(RD-3A-01 resolved) AS-IS <c>StartOfMainAttack</c>
    /// (GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs): until the owner's turn ends, at the start of the
    /// owner's main phase this Digimon MUST attack (the offer cannot be declined — <c>SetCanNotSelectNotAttack</c>).
    /// Builds an inline <see cref="ActivateClass"/> that drives a mandatory <see cref="SelectAttackEffect"/> offer
    /// and adds a <c>GetCardEffect</c> selector to <see cref="Permanent.UntilOwnerTurnEndEffects"/> that yields the
    /// ActivateClass only at <see cref="EffectTiming.OnStartMainPhase"/> — surfaced by the main-phase auto-fire
    /// window (see file header). No await in the grant body (the AS-IS UI debuff yield is stripped).</summary>
    public static Task StartOfMainAttack(Permanent targetPermanent, ICardEffect cardEffect)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Attack with this Digimon", CanUseCondition, targetPermanent.TopCard);
        activateClass.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription());
        activateClass.SetEffectSourcePermanent(targetPermanent);
        targetPermanent.UntilOwnerTurnEndEffects.Add(GetCardEffect);

        string EffectDiscription()
        {
            return "[Start of Your Main Phase] Attack with this Digimon.";
        }

        bool CanUseCondition(Hashtable hashtable1)
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (new Player(targetPermanent.TopCard.Context, targetPermanent.TopCard.Owner).GetBattleAreaDigimons().Contains(targetPermanent))
                {
                    if (IsOwnerTurn(targetPermanent.TopCard))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanActivateCondition1(Hashtable hashtable1)
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(cardEffect))
                {
                    if (targetPermanent.CanAttack(activateClass))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task ActivateCoroutine1(Hashtable _hashtable1)
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (targetPermanent.CanAttack(activateClass))
                {
                    SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                    selectAttackEffect.SetUp(
                        attacker: targetPermanent,
                        canAttackPlayerCondition: () => true,
                        defenderCondition: (permanent) => true,
                        cardEffect: activateClass);

                    selectAttackEffect.SetCanNotSelectNotAttack();

                    await selectAttackEffect.Activate();
                }
            }
        }

        ICardEffect GetCardEffect(EffectTiming _timing)
        {
            if (_timing == EffectTiming.OnStartMainPhase) return activateClass;
            return null;
        }

        // AS-IS tail `yield return CreateDebuffEffect(targetPermanent)` = UI debuff animation (stripped).
        return Task.CompletedTask;
    }
}
