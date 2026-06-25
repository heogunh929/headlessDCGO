namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class ModifierHelperFactory
{
    public static NumericModifier ChangeDp(string id, int value, HeadlessEntityId? targetEntityId = null)
    {
        return NumericModifier.Add(id, NumericModifierMetric.Dp, value, targetEntityId);
    }

    public static NumericModifier ChangeBaseDp(string id, int value, HeadlessEntityId? targetEntityId = null)
    {
        return NumericModifier.Add(id, NumericModifierMetric.BaseDp, value, targetEntityId);
    }

    public static NumericModifier ChangePlayCost(string id, int value)
    {
        return NumericModifier.Add(id, NumericModifierMetric.PlayCost, value);
    }

    public static NumericModifier SetPlayCost(string id, int value)
    {
        return NumericModifier.Set(id, NumericModifierMetric.PlayCost, value);
    }

    public static NumericModifier ChangeDigivolutionCost(string id, int value)
    {
        return NumericModifier.Add(id, NumericModifierMetric.DigivolutionCost, value);
    }

    public static NumericModifier SetDigivolutionCost(string id, int value)
    {
        return NumericModifier.Set(id, NumericModifierMetric.DigivolutionCost, value);
    }

    public static NumericModifier ChangeSecurityAttack(string id, int value, HeadlessEntityId? targetEntityId = null)
    {
        return NumericModifier.Add(id, NumericModifierMetric.SecurityAttack, value, targetEntityId);
    }

    public static NumericModifier InvertSecurityAttack(string id, int value, HeadlessEntityId? targetEntityId = null)
    {
        return NumericModifier.InvertSecurityAttack(id, value, targetEntityId);
    }
}
