// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/IsDigivolvedByTheEffect.cs
// (R4 S3b-2②) 1:1 — unblocked by the just-after bookkeeping store (Permanent.DigivolvingEffect landed).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public partial class CardEffectCommons
{
    public static bool IsDigivolvedByTheEffect(Permanent permanent, CardSource cardSource, ICardEffect cardEffect)
    {
        if (IsPermanentExistsOnBattleArea(permanent))
        {
            // AS-IS `permanent.TopCard == cardSource` — mirror CardSource is a per-access view, so reference
            // equality is expressed as instance identity (the established view-comparison adaptation).
            if (permanent.TopCard != null && cardSource != null && permanent.TopCard.InstanceId == cardSource.InstanceId)
            {
                if (permanent.DigivolvingEffect == cardEffect)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
