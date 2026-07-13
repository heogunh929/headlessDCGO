// Source: DCGO/Assets/Scripts/Script/CardEffectInterfaces.cs
// (EFFECT-MODEL REBUILD / FOUNDATION) 1:1 mirror of the original 74 marker interfaces a card effect class
// (`: ICardEffect, ISomeMarkerEffect`) opts into to participate in a continuous/replacement/query scan, PLUS
// the AS-IS `CEntity_Effect` base class (DCGO/Assets/Scripts/Script/CEntity_Effect.cs) — moved here because
// this is the file that used to hold the mirror's OWN (structurally different, pre-rebuild) `CEntity_Effect`.
//
// COLLISION NOTE: the OLD mirror `ICardEffect { EffectBinding ToBinding(string) }` interface,
// `IActivatedCardEffect : ICardEffect`, and `abstract class CEntity_Effect { CardEffects }` used to live at
// Assets/Scripts/Script/CardEffectCommons/CardEffectInterfaces.cs. `ICardEffect` is now the abstract class in
// ICardEffect.cs (this directory) — the two `ICardEffect`s collided in the same namespace, so the old trio was
// REMOVED from that file (see its updated header) and `CEntity_Effect` re-ported here, AS-IS-shaped. This is the
// EXPECTED big-bang cascade: every one of the ~85 headless primitives implementing the old `ICardEffect.ToBinding`
// interface, and every caller of the OLD `CEntity_Effect` (CardEffectDispatch.TryCreateForCard, CardEffectRegistrar,
// CardSource.LinkConditionOf/AppFusionConditionOf/AssemblyConditionOf) now fails to compile — not fixed in this
// FOUNDATION pass (see docs/audit/rebuild_p1_missing.md).
//
// Namespace: `...Script.CardEffectCommons`, matching ICardEffect.cs (this goal) — every interface here is
// expressed purely in terms of the already-ported foundation types (CardSource/Permanent/Player/ICardEffect/
// EffectTiming) that live in that namespace.
//
// MISSING TYPES (referenced verbatim, AS-IS-named, not defined anywhere on the mirror yet — see MISSING.md):
// `CardColor` (IChangeCardColorEffect/IChangeBaseCardColorEffect — the mirror models colors as
// `IReadOnlyList<string>` instead, see CardSource.CardColors/BaseCardColors), `JogressCondition`
// (IAddJogressConditionEffect), `DigiXrosCondition` (IAddDigiXrosConditionEffect),
// `BurstDigivolutionCondition` (IAddBurstDigivolutionConditionEffect). `AssemblyCondition`/`LinkCondition`/
// `AppFusionCondition` already exist (CardEffectCommons/Conditions.cs); `SelectCardEffect.Root` already exists
// (Assets/Scripts/Script/SelectCardEffect.cs, `...Script` namespace — `using`d below); `CardEffectCommons.
// IgnoreRequirement` already exists (CardEffectCommons/CardEffectCommons.cs).
//
// `IOptionResolutionEffect.Resolve`: AS-IS `IEnumerator Resolve(CardSource)` -> `Task Resolve(CardSource)`,
// consistent with the ICardEffect.cs Activate-method translation (coroutine action, not a sync gate).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

#region "Target effect is negated" effect.
// AS-IS CardEffectInterfaces.cs:6-9.
public interface IDisableCardEffect
{
    bool IsDisabled(ICardEffect cardEffect);
}
#endregion

#region "Target permanent can not be selected by the effect" effect.
// AS-IS CardEffectInterfaces.cs:13-16.
public interface ICanNotSelectBySkillEffect
{
    bool CanNotSelectBySkill(Permanent permanent, ICardEffect cardEffect);
}
#endregion

#region "Target card cannot be played" effect.
// AS-IS CardEffectInterfaces.cs:20-23.
public interface ICanNotPlayCardEffect
{
    bool CanNotPlay(CardSource cardSource);
}
#endregion

#region "Ignore target card's color requirement" effect.
// AS-IS CardEffectInterfaces.cs:27-30.
public interface IIgnoreColorConditionEffect
{
    bool IgnoreColorCondition(CardSource cardSource);
}
#endregion

#region "Target permanent gains Iceclad" effect
// AS-IS CardEffectInterfaces.cs:34-37.
public interface IIcecladEffect
{
    bool HasIceclad(Permanent permanent);
}
#endregion

#region "Target permanent gains Rush" effect
// AS-IS CardEffectInterfaces.cs:41-44.
public interface IRushEffect
{
    bool HasRush(Permanent permanent);
}
#endregion

#region "Target permanent gains Reboot" effect
// AS-IS CardEffectInterfaces.cs:48-51.
public interface IRebootEffect
{
    bool HasReboot(Permanent permanent);
}
#endregion

#region "Target permanent gains Scapegoat" effect
// AS-IS CardEffectInterfaces.cs:55-58.
public interface IScapegoatEffect
{
    bool HasScapegoat(Permanent permanent);
}
#endregion

#region "Treat target permanent as Digimon" effect
// AS-IS CardEffectInterfaces.cs:62-65.
public interface ITreatAsDigimonEffect
{
    bool IsDigimon(Permanent permanent);
}
#endregion

#region "Change target card's colors" effect
// AS-IS CardEffectInterfaces.cs:69-72. MISSING type: CardColor (see file header).
public interface IChangeCardColorEffect
{
    List<CardColor> GetCardColors(List<CardColor> CardColors, CardSource cardSource);
}
#endregion

#region "Change target card's origin colors" effect
// AS-IS CardEffectInterfaces.cs:76-79. MISSING type: CardColor (see file header).
public interface IChangeBaseCardColorEffect
{
    List<CardColor> GetBaseCardColors(List<CardColor> BaseCardColors, CardSource cardSource);
}
#endregion

#region "Target card gains additional effects" effect
// AS-IS CardEffectInterfaces.cs:83-87.
public interface IAddSkillEffect
{
    bool ShouldAddEffect(EffectTiming timing);
    List<ICardEffect> GetCardEffect(CardSource card, List<ICardEffect> GetCardEffects, EffectTiming timing);
}
#endregion

#region Label additional text to appear in Permanent Details
// AS-IS CardEffectInterfaces.cs:91-96.
public interface IAddDetailEffect
{
    bool PermanentCondition(Permanent permanent);
    string GetDetail();//Used to avoid any interactions already built in to effect name
    bool TriggerEffect();//Unused for now, but could be used to display something on the card to indicate it has gained an additional triggered effect, eg. [When Attacking] Lose 3 Memory
}
#endregion

#region "Target card cannot be affected by effects" effect
// AS-IS CardEffectInterfaces.cs:100-103.
public interface ICanNotAffectedEffect
{
    bool CanNotAffect(CardSource cardSource, ICardEffect cardEffect);
}
#endregion

#region "Target card cannot be trashed from digivolution cards" effect
// AS-IS CardEffectInterfaces.cs:107-110.
public interface ICanNotTrashFromDigivolutionCardsEffect
{
    bool CanNotTrashFromDigivolutionCards(CardSource cardSource, ICardEffect cardEffect);
}
#endregion

#region "Change target permanent's Security Attack" effect
// AS-IS CardEffectInterfaces.cs:114-119.
public interface IChangeSAttackEffect
{
    int GetSAttack(int SAttack, Permanent permanent, int invertValue);
    CalculateOrder isUpDown();
    bool PermanentCondition(Permanent permanent);
}
#endregion

#region "Change target permanent's Link Max" effect
// AS-IS CardEffectInterfaces.cs:123-128.
public interface IChangeLinkMaxEffect
{
    int GetLinkMax(int linkMax, Permanent permanent, int invertValue);
    CalculateOrder isUpDown();
    bool PermanentCondition(Permanent permanent);
}
#endregion

#region "Invert target permanent's Security Attack" effect
// AS-IS CardEffectInterfaces.cs:132-136.
public interface IInvertSAttackEffect
{
    int InversionValue(Permanent permanent, int invertValue);
    bool PermanentCondition(Permanent permanent);
}
#endregion

#region "Change target card's card names" effect
// AS-IS CardEffectInterfaces.cs:140-143.
public interface IChangeCardNamesEffect
{
    List<string> ChangeCardNames(List<string> CardNames, CardSource cardSource);
}
#endregion

#region "Change target card's origin card names" effect
// AS-IS CardEffectInterfaces.cs:147-150.
public interface IChangeBaseCardNameEffect
{
    List<string> ChangeBaseCardNames(List<string> BaseCardNames, CardSource cardSource);
}
#endregion

#region "Change target card's card names for DigiXros" effect
// AS-IS CardEffectInterfaces.cs:154-157.
public interface IChangeCardNamesForDigiXrosEffect
{
    List<string> ChangeCardNamesForDigiXros(List<string> CardNames, CardSource cardSource);
}
#endregion

#region "Change target card's card level for Assembly" effect
// AS-IS CardEffectInterfaces.cs:161-164.
public interface IChangeCardLevelForAssemblyEffect
{
    List<int> ChangeCardLevelForAssembly(List<int> levels, CardSource cardSource);
}
#endregion

#region "Change target card's traits" effect
// AS-IS CardEffectInterfaces.cs:168-171.
public interface IChangeTraitsEffect
{
    List<string> ChangTraits(List<string> Traits, CardSource cardSource);
}
#endregion

#region "Target permanent cannot unsuspend" effect
// AS-IS CardEffectInterfaces.cs:175-178.
public interface ICanNotUnsuspendEffect
{
    bool CanNotUnsuspend(Permanent permanent);
}
#endregion

#region "Target permanent cannot suspend" effect
// AS-IS CardEffectInterfaces.cs:182-185.
public interface ICanNotSuspendEffect
{
    bool CanNotSuspend(Permanent permanent);
}
#endregion

#region "Target permanent cannot return to hand" effect
// AS-IS CardEffectInterfaces.cs:189-192.
public interface ICannotReturnToHandEffect
{
    bool CannotReturnToHand(Permanent permanent, ICardEffect cardEffect);
}
#endregion

#region "Target permanent cannot return to deck" effect
// AS-IS CardEffectInterfaces.cs:196-199.
public interface ICannotReturnToLibraryEffect
{
    bool CannotReturnToLibrary(Permanent permanent, ICardEffect cardEffect);
}
#endregion

#region "Target permanent cannot affected by DP minus effect" effect
// AS-IS CardEffectInterfaces.cs:203-206.
public interface IImmuneFromDPMinusEffect
{
    bool ImmuneFromDPMinus(Permanent permanent, ICardEffect cardEffect);
}
#endregion

#region "Target permanent cannot affected by De-Digivolve" effect
// AS-IS CardEffectInterfaces.cs:210-213.
public interface IImmuneFromDeDigivolveEffect
{
    bool ImmuneDeDigivolve(Permanent permanent);
}
#endregion

#region "Target permanent cannot have its stack cards trashed" effect
// AS-IS CardEffectInterfaces.cs:217-220.
public interface IImmuneFromStackTrashingEffect
{
    bool ImmuneStackTrashing(Permanent permanent, ICardEffect effect);
}
#endregion

#region "Target permanent does not have DP" effect
// AS-IS CardEffectInterfaces.cs:224-227.
public interface IDontHaveDPEffect
{
    bool DontHaveDP(Permanent permanent);
}
#endregion

#region "Change target permanent's DP" effect
// AS-IS CardEffectInterfaces.cs:231-237.
public interface IChangeDPEffect
{
    int GetDP(int DP, Permanent permanent);
    bool PermanentCondition(Permanent permanent);
    bool IsUpDown();
    bool IsMinusDP();
}
#endregion

#region "Change target permanent's origin DP" effect
// AS-IS CardEffectInterfaces.cs:241-247.
public interface IChangeBaseDPEffect
{
    int GetDP(int DP, Permanent permanent);
    bool PermanentCondition(Permanent permanent);
    bool IsUpDown();
    bool IsMinusDP();
}
#endregion

#region "Change target card's DP" effect
// AS-IS CardEffectInterfaces.cs:251-257.
public interface IChangeCardDPEffect
{
    int GetDP(int DP, CardSource cardSource);
    bool CardCondition(CardSource cardSource);
    bool IsUpDown();
    bool IsMinusDP();
}
#endregion

#region "Change target card's cost" effect
// AS-IS CardEffectInterfaces.cs:261-268.
public interface IChangeCostEffect
{
    int GetCost(int cost, CardSource cardSource, SelectCardEffect.Root root, List<Permanent> targetPermanents);
    bool CardCondition(CardSource cardSource);
    bool IsUpDown();
    bool IsCheckAvailability();
    bool IsChangePayingCost();
}
#endregion

#region "Change target card's cost" effect
// AS-IS CardEffectInterfaces.cs:271-278.
public interface IChangeLinkCostEffect
{
    int GetCost(int cost, CardSource cardSource, Permanent permanent, SelectCardEffect.Root root);
    bool CardCondition(CardSource cardSource);
    bool PermanentCondition(Permanent permanent);
    bool IsUpDown();
}
#endregion

#region "Change the maximum DP of DP-based deletion effect" effect
// AS-IS CardEffectInterfaces.cs:282-285.
public interface IChangeDPDeleteEffectMaxDPEffect
{
    int GetMaxDP(int maxDP, ICardEffect cardEffect);
}
#endregion

#region "Change target permanent's level" effect
// AS-IS CardEffectInterfaces.cs:289-292.
public interface IChangePermanentLevelEffect
{
    int GetPermanentLevel(int level, Permanent permanent);
}
#endregion

#region "Change target card's level" effect
// AS-IS CardEffectInterfaces.cs:296-299.
public interface IChangeCardLevelEffect
{
    int GetCardLevel(int level, CardSource cardSource);
}
#endregion

#region "Target attacking permanent can attack to target defending permanent" effect
// AS-IS CardEffectInterfaces.cs:303-306.
public interface ICanAttackTargetDefendingPermanentEffect
{
    bool CanAttackTargetDefendingPermanent(Permanent Attacker, Permanent Defender, ICardEffect cardEffect);
}
#endregion

#region "Target attacking permanent cannot attack to target defending permanent" effect
// AS-IS CardEffectInterfaces.cs:310-313.
public interface ICanNotAttackTargetDefendingPermanentEffect
{
    bool CanNotAttackTargetDefendingPermanent(Permanent Attacker, Permanent Defender);
}
#endregion

#region "Target Security Digimon card does not battle" effect
// AS-IS CardEffectInterfaces.cs:317-320.
public interface IDontBattleSecurityDigimonEffect
{
    bool DontBattleSecurityDigimon(CardSource cardSource);
}
#endregion

#region "Target defending permanent cannot block target attacking permanent" effect"
// AS-IS CardEffectInterfaces.cs:324-327.
public interface ICannotBlockEffect
{
    bool CannotBlock(Permanent AttackingPermanent, Permanent BlockingPermanent);
}
#endregion

#region "Target permanent gets additional digivolution condition" effect
// AS-IS CardEffectInterfaces.cs:331-334. `CardEffectCommons.IgnoreRequirement` is the enum NESTED in the
// CardEffectCommons class (CardEffectCommons.cs) — 1:1 with AS-IS.
public interface IAddDigivolutionRequirementEffect
{
    int GetEvoCost(Permanent permanent, CardSource cardSource, CardEffectCommons.IgnoreRequirement ignore, bool checkAvailability);
}
#endregion

#region "Target player cannot gain Memory by effects" effect
// AS-IS CardEffectInterfaces.cs:338-341.
public interface ICannotAddMemoryEffect
{
    bool cannotAddMemory(Player player, ICardEffect cardEffect);
}
#endregion

#region "Target player cannot reduce digivolution costs" effect
// AS-IS CardEffectInterfaces.cs:345-348.
public interface ICannotReduceCostEffect
{
    bool CannotReduceCost(Player player, List<Permanent> targetPermanents, CardSource cardSource);
}
#endregion

#region "Target player cannot ignore digivolution condition" effect
// AS-IS CardEffectInterfaces.cs:352-355.
public interface ICannotIgnoreDigivolutionConditionEffect
{
    bool cannotIgnoreDigivolutionCondition(Player player, Permanent targetPermanent, CardSource cardSource);
}
#endregion

#region "Target player cannot add Security" effect
// AS-IS CardEffectInterfaces.cs:359-362.
public interface ICannotAddSecurityEffect
{
    bool cannotAddSecurity(Player player, ICardEffect cardEffect);
}
#endregion

#region "Target Digimon can be suspended by Digisorption" effect
// AS-IS CardEffectInterfaces.cs:366-370.
public interface ICanSuspendByDigisorptionEffect
{
    bool canSuspendDigisorption(Permanent permanent, ICardEffect cardEffect);
    bool isCheckAvailability();
}
#endregion

#region "Target permanent cannot digivolve into target card" effect"
// AS-IS CardEffectInterfaces.cs:374-377.
public interface ICanNotDigivolveEffect
{
    bool CanNotEvolve(Permanent permanent, CardSource cardSource);
}
#endregion

#region "Target card cannot be played as a new permanent" effect
// AS-IS CardEffectInterfaces.cs:381-384.
public interface ICanNotPutFieldEffect
{
    bool CanNotPutField(CardSource cardSource, ICardEffect cardEffect);
}
#endregion

#region "Target card cannot be moved" effect
// AS-IS CardEffectInterfaces.cs:388-391.
public interface ICanNotMoveEffect
{
    bool CanNotMove(CardSource cardSource, ICardEffect cardEffect);
}
#endregion

#region "Target card gains DNA digivolution conditions" effect
// AS-IS CardEffectInterfaces.cs:395-398. MISSING type: JogressCondition (see file header).
public interface IAddJogressConditionEffect
{
    JogressCondition GetJogressCondition(CardSource cardSource);
}
#endregion

#region "Target card gains DigiXros conditions" effect
// AS-IS CardEffectInterfaces.cs:402-405. MISSING type: DigiXrosCondition (see file header).
public interface IAddDigiXrosConditionEffect
{
    DigiXrosCondition GetDigiXrosCondition(CardSource cardSource);
}
#endregion

#region "Target card gains Assembly conditions" effect
// AS-IS CardEffectInterfaces.cs:409-412.
public interface IAddAssemblyConditionEffect
{
    AssemblyCondition GetAssemblyCondition(CardSource cardSource);
}
#endregion

#region "Target card gains Burst digivolution conditions" effect
// AS-IS CardEffectInterfaces.cs:416-419. MISSING type: BurstDigivolutionCondition (see file header).
public interface IAddBurstDigivolutionConditionEffect
{
    BurstDigivolutionCondition GetBurstDigivolutionCondition(CardSource cardSource);
}
#endregion

#region "Target card gains Link conditions" effect
// AS-IS CardEffectInterfaces.cs:423-426.
public interface IAddLinkConditionEffect
{
    LinkCondition GetLinkCondition(CardSource cardSource);
}
#endregion

#region "Target card gains App Fusion digivolution conditions" effect
// AS-IS CardEffectInterfaces.cs:430-433.
public interface IAddAppFusionConditionEffect
{
    AppFusionCondition GetAppFusionCondition(CardSource cardSource);
}
#endregion

#region "Add the maximum number of cards that can be selected from trash in DigiXros" effect
// AS-IS CardEffectInterfaces.cs:437-440.
public interface IAddMaxTrashCountDigiXrosEffect
{
    int GetMaxTrashCount(CardSource cardSource);
}
#endregion

#region "Add the maximum number of cards that can be selected from under Tamer in DigiXros" effect
// AS-IS CardEffectInterfaces.cs:444-447.
public interface IAddMaxUnderTamerCountDigiXrosEffect
{
    int getMaxUnderTamerCount(CardSource cardSource);
}
#endregion

#region "Target card be selected in DigiXros" effect
// AS-IS CardEffectInterfaces.cs:451-454.
public interface ICanSelectDigiXrosEffect
{
    bool CanSelect(CardSource cardSource, Permanent permanent);
}
#endregion

#region "Target card be selected in Assembly" effect
// AS-IS CardEffectInterfaces.cs:458-461.
public interface ICanSelectAssemblyEffect
{
    bool CanSelect(CardSource cardSource, Permanent permanent);
}
#endregion

#region "Target permanent's attack target cannot be switched" effect"
// AS-IS CardEffectInterfaces.cs:465-468.
public interface ICanNotSwitchAttackTargetEffect
{
    bool CanNotBeSwitchAttackTarget(Permanent permanent);
}
#endregion

#region "Target permanent cannot be deleted" effect"
// AS-IS CardEffectInterfaces.cs:472-475.
public interface ICanNotBeDestroyedEffect
{
    bool CanNotBeDestroyed(Permanent permanent);
}
#endregion

#region "Target permanent cannot be deleted by battle" effect"
// AS-IS CardEffectInterfaces.cs:479-484.
public interface ICanNotBeDestroyedByBattleEffect
{
    bool CanNotBeDestroyedByBattle(Permanent permanent, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard);
    bool PermanentCondition(Permanent permanent);
}
#endregion

#region "Target permanent cannot be deleted by effect" effect"
// AS-IS CardEffectInterfaces.cs:487-490.
public interface ICanNotBeDestroyedBySkillEffect
{
    bool CanNotBeDestroyedBySkill(Permanent permanent, ICardEffect cardEffect);
}
#endregion

#region "Target permanent cannot be removed" effect"
// AS-IS CardEffectInterfaces.cs:493-497.
public interface ICanNotBeRemovedEffect
{
    bool CanNotBeRemoved(Permanent permanent);
}
#endregion

#region "Target permanent gains Blocker" effect
// AS-IS CardEffectInterfaces.cs:500-504.
public interface IBlockerEffect
{
    bool IsBlocker(Permanent permanent);
}
#endregion

#region "Add levels for DNA digivolution" effect
// AS-IS CardEffectInterfaces.cs:507-511.
public interface IAddJogressLevelsEffect
{
    List<int> GetJogressLevels(CardSource cardSource, Permanent permanent);
}
#endregion

#region "Add names for DNA digivolution" effect
// AS-IS CardEffectInterfaces.cs:514-518.
public interface IAddDNANamesEffect
{
    List<string> GetDNANames(CardSource cardSource, Permanent permanent);
}
#endregion

#region "Change the min memory to end turn" effect
// AS-IS CardEffectInterfaces.cs:521-525.
public interface IChangeEndTurnMinMemoryEffect
{
    int GetMinMemory(int minMemory);
}
#endregion

#region "Vortex may attack Players" effect
// AS-IS CardEffectInterfaces.cs:528-532.
public interface IVortexCanAttackPlayersEffect
{
    bool VortexCanAttackPlayersPermanent(Permanent Attacker);
}
#endregion

#region "Instead of trashing after use" effects
// AS-IS CardEffectInterfaces.cs:536-540. `IEnumerator Resolve` -> `Task Resolve` (see file header).
public interface IOptionResolutionEffect
{
    bool CanResolve(CardSource optionCard);
    Task Resolve(CardSource optionCard);
}
#endregion

#region Collision
// AS-IS CardEffectInterfaces.cs:544-547.
public interface ICollisionEffect
{
    bool HasCollision(Permanent permanent);
}
#endregion

// ============================================================================================================
// Source: DCGO/Assets/Scripts/Script/CEntity_Effect.cs (a separate AS-IS file; ported here — see COLLISION NOTE
// in this file's header for why). `abstract partial class CEntity_Effect : MonoBehaviourPunCallbacks` -> plain
// `abstract class` (Photon/MonoBehaviour stripped). `.Filter(predicate)` (DCGO IEnumerableExtension.cs:44, a
// `Where(...).ToList()` shim) referenced verbatim — MISSING.md (IEnumerableExtension is itself an unported
// skeleton, out of this FOUNDATION goal's file list).
// ============================================================================================================

#region CEntity_Effect

/// <summary>AS-IS <c>CEntity_Effect</c> (CEntity_Effect.cs:8) — the base class every ported card's effect
/// component derives from (`class BT1_001 : CEntity_Effect`), overriding <see cref="CardEffects"/>.</summary>
public abstract class CEntity_Effect
{
    // AS-IS CEntity_Effect.cs:10-13.
    public virtual List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource)
    {
        return new List<ICardEffect>();
    }

    // AS-IS CEntity_Effect.cs:15-18.
    public List<ICardEffect> GetCardEffects(EffectTiming timing, CardSource cardSource)
    {
        return CardEffects(timing, cardSource).Filter(cardEffect => cardEffect != null);
    }

    //後で消す (AS-IS comment: "delete this later")
    // AS-IS CEntity_Effect.cs:21-29. ADAPTATION: AS-IS `card.PermanentOfThisCard() != null`; the mirror
    // `CardSource.PermanentOfThisCard()` returns a non-null `PermanentView` sentinel (see ICardEffect.cs file
    // header, adaptation (2)) — `!card.PermanentOfThisCard().IsEmpty` is the equivalent liveness check.
    public static bool isExistOnField(CardSource card)
    {
        if (!card.PermanentOfThisCard().IsEmpty)
        {
            return true;
        }

        return false;
    }
}

#endregion
