// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Vortex.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Vortex). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Vortex's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateVortex). The LIVE "this Digimon makes an effect-driven attack" path is the
// S1 hub EffectDrivenAttack — this branch is the grant/mirror layer (resolving emits GrantVortex).
// AS-IS Vortex target (DataBase.VortexEffectDiscription + VortexProcess): "At the end of your turn, this
// Digimon may attack an opponent's DIGIMON (any — defenderCondition _ => true with SetIsVortex; NOT the
// player), can attack the turn it was played." Player-target requires a SEPARATE VortexCanAttackPlayers
// effect, not the base keyword. GR-006 wires this via EndOfTurnEffectAttack -> EffectDrivenAttack with
// EffectAttackOptions(AllowDigimonTarget:true, TargetUnsuspended:true, AllowPlayerTarget:FALSE).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveVortex(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            // AS-IS CanActivateVortex: this Digimon is on the battle area (the dispatch already enforces battle
            // area). The full "can attack a Digimon or player" eligibility is enforced live by
            // EffectDrivenAttack.GetTargets when the attack is offered.
            return CardEffectCanResolveResult.Success("Vortex can initiate an effect-driven attack.", BaseValues(context, target));
        }
    }
}

// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons Hashtable-based siblings
// (KeyWordEffects/Vortex.cs) — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
// Both are genuine STOPs: AS-IS CanActivateVortex/VortexProcess depend on Permanent.CanAttack/
// CanAttackTargetDigimon (the general attack-eligibility gate, Permanent.cs:2090/2214 — a large keyword-aware
// method never ported to the mirror Permanent) and SelectAttackEffect (no mirror component; GManager.GetComponent
// only supports SelectPermanentEffect/SelectCardEffect/OptionalSkill). Both unported, out of this cluster's
// scope (Permanent.cs is not a KeyWordEffects/CanUseEffects/kind-class/DataBase file). The LIVE Vortex path is
// already fully implemented independently via EndOfTurnEffectAttack + EffectDrivenAttack (see this file's own
// header), so this old-model ActivateClass path (still exercised by EX8_074/TfxVortex for keyword-grant
// REGISTRATION purposes only) is dead-relative to the actual attack resolution — design item RD-P6C2-8.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;

    public static partial class CardEffectCommons
    {
        /// <summary>AS-IS <c>CanActivateVortex</c> (KeyWordEffects/Vortex.cs:7). STOP — design item RD-P6C2-8.</summary>
        public static bool CanActivateVortex(CardSource cardSource, ICardEffect activateClass)
        {
            throw new NotSupportedException(
                "CanActivateVortex: AS-IS Permanent.CanAttack/CanAttackTargetDigimon have no mirror — design item " +
                "RD-P6C2-8, docs/audit/rebuild_p6_cluster2_notes.md.");
        }

        /// <summary>AS-IS <c>VortexProcess</c> (KeyWordEffects/Vortex.cs:56). STOP — design item RD-P6C2-8.</summary>
        public static System.Threading.Tasks.Task VortexProcess(CardSource cardSource, ICardEffect activateClass)
        {
            throw new NotSupportedException(
                "VortexProcess: AS-IS Permanent.CanAttack/SelectAttackEffect have no mirror — design item " +
                "RD-P6C2-8, docs/audit/rebuild_p6_cluster2_notes.md.");
        }
    }
}
