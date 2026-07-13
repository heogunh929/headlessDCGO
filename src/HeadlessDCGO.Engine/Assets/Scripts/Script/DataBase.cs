// Source: DCGO/Assets/Scripts/Script/DataBase.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>(EFFECT-MODEL REBUILD) Minimal mirror of AS-IS <c>DataBase</c> (DCGO Script/DataBase.cs) — the
/// large static card-data / text service. Only the members ported effect code needs so far are mirrored here,
/// grown member-by-member as ports require (not ported wholesale). Current members: <see cref="ReplaceToASCII"/>.
/// (File lives at the AS-IS path <c>Script/DataBase.cs</c>; namespace kept <c>...CardEffectCommons</c> so
/// existing references are unaffected — a later namespace-normalisation pass, not a path concern.)</summary>
public static class DataBase
{
    /// <summary>1:1 mirror of AS-IS <c>DataBase.ReplaceToASCII</c> (DataBase.cs:567): normalises full-width /
    /// Japanese punctuation in effect descriptions to ASCII so the <c>[On Play]</c>-style prefix checks in
    /// <see cref="ICardEffect.IsOnPlay"/> etc. match. Verbatim replacement set.</summary>
    public static string ReplaceToASCII(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text
            .Replace("＜", "<")
            .Replace("＞", ">")
            .Replace("、", ",")
            .Replace("，", ",")
            .Replace("“", "\"")
            .Replace("・", "")
            .Replace("　", "")
            .Replace("！", "!");
    }

    /// <summary>(bridge W5) 1:1 mirror of AS-IS <c>DataBase.IsXAntibodyString</c> (DataBase.cs:440): the
    /// space/hyphen-insensitive "X Antibody" trait/name normaliser <c>CardSource.HasXAntibodyTraits</c> reads.
    /// Verbatim (pure string helper).</summary>
    public static bool IsXAntibodyString(string text) =>
    !string.IsNullOrEmpty(text) && text.Replace(" ", "").Replace("-", "").ToLower() == "xantibody";

    // (P6 cluster2) 1:1 mirror of the AS-IS `*EffectDiscription`/`*EffectDescription` keyword text helpers
    // (DataBase.cs:446-566) — pure string constants the CardEffectFactory KeyWordEffects partials pass to
    // ActivateClass.SetUpActivateClass as the effect's display description. Verbatim.

    public static string BlockerEffectDiscription()
    {
        return "<Blocker> (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)";
    }

    public static string RebootEffectDiscription()
    {
        return "<Reboot> (Unsuspend this Digimon during your opponent's unsuspend phase.)";
    }

    public static string PierceEffectDiscription()
    {
        return "<Piercing> (When this Digimon attacks and deletes an opponent's Digimon and survives the battle, it performs any security checks it normally would.)";
    }

    public static string RetaliationEffectDiscription()
    {
        return "<Retaliation> (When this Digimon is deleted after losing a battle, delete the Digimon it was battling.)";
    }

    public static string BilitzEffectDiscription()
    {
        return "<Blitz> (This Digimon can attack when your opponent has 1 or more memory.)";
    }

    public static string ArmorPurgeEffectDiscription()
    {
        return "<Armor Purge> (When this Digimon would be deleted, you may trash the top card of this Digimon to prevent that deletion.)";
    }

    public static string SaveEffectDiscription()
    {
        return "[On Deletion] <Save> (You may place this card under one of your Tamers.)";
    }

    public static string EvadeEffectDiscription()
    {
        return "<Evade> (When this Digimon would be deleted, you may suspend it to prevent that deletion.)";
    }

    public static string RaidEffectDiscription()
    {
        return "<Raid> (When this Digimon attacks, you may switch the target of attack to 1 of your opponent's unsuspended Digimon with the highest DP.)";
    }

    public static string BarrierEffectDiscription()
    {
        return "<Barrier> (When this Digimon would be deleted in battle, by trashing the top card of your security stack, prevent that deletion.)";
    }

    public static string BlastDigivolveEffectDiscription()
    {
        return "[Hand] [Counter] <Blast Digivolve> (Your Digimon may digivolve into this card without paying the cost.)";
    }

    public static string BlastDNADigivolveEffectDiscription()
    {
        return "[Hand] [Counter] <Blast DNA Digivolve> (One of your specified Digimon and 1 of the specified card in the hand may DNA Digivolve into this card.)";
    }

    public static string FortitudeEffectDiscription()
    {
        return "<Fortitude> (When this Digimon with digivolution cards is deleted, play this card without paying the cost.)";
    }

    public static string AllianceEffectDiscription()
    {
        return "<Alliance> (When this Digimon attacks, by suspending 1 of your other Digimon, this Digimon adds the suspended Digimon's DP and gains <Security Attack +1> for the attack.)";
    }

    public static string AscensionEffectDescription()
    {
        return "<Ascension> (When this Digimon is deleted, you may place this card as the top security card.)";
    }

    public static string PartitionEffectDiscription()
    {
        return "<Partition> (When this Digimon with 1 of each specified card in its digivolution cards would leave the battle area other than by one of your effects or in battle, you may play 1 of each card without paying their costs.)";
    }

    public static string CollisionEffectDiscription()
    {
        return "<Collision> (During this Digimon's attack, all of your opponent's Digimon gain <Blocker>, and your opponent blocks if possible.)";
    }

    public static string VortexEffectDiscription()
    {
        return "<Vortex> (At the end of your turn, this Digimon may attack an opponent's Digimon. With this effect, it can attack the turn it was played.)";
    }

    public static string OverclockEffectDiscription(string trait)
    {
        return $"<Overclock [{trait}]> (At the end of your turn, by deleting 1 of your Tokens or other [{trait}] trait Digimon, this Digimon attacks a player without suspending.)";
    }

    public static string TrainingEffectDiscription()
    {
        return "<Training> (In the main phase, by suspending this Digimon, place your deck's top card face down as this Digimon's bottom digivolution card. This effect can also activate in the breeding area).";
    }

    public static string DecodeEffectDiscription(string[] decodeStrings)
    {
        return $"<Decode {decodeStrings[0]}> (When this Digimon would leave the battle area other than in battle, you may play 1 {decodeStrings[1]} Digimon card from its digivolution cards without paying the cost.)";
    }

    public static string ExecuteEffectDiscription()
    {
        return "<Execute> (At the end of your turn, this Digimon may attack. At the end of that attack, delete this Digimon. Your opponent's unsuspended Digimon can also be attacked with this effect.)";
    }

    public static string ProgressEffectDiscription()
    {
        return "<Progress> (While attacking, your opponent's effects don't affect this Digimon.)";
    }

    public static string LinkEffectDiscription()
    {
        return "[Link] (Plug this card from the hand or battle area sideways into the specified Digimon in the battle area.)";
    }
}
