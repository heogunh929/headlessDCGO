// Source: DCGO/Assets/Scripts/Script/PlayerSelection/ValueSelection.cs
// 1:1 mirror, verbatim: the int/bool payload the AS-IS player selection queue carries
// (ValueSelection.cs:1-24 — the public field `_value`, both constructors, ValueAsInt, ValueAsBool).
// Verified in the original: a bool rides the int channel as `value ? 1 : 0` / `_value != 0`.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.PlayerSelection;

public class ValueSelection : IPlayerSelection
{
    public int _value;

    public ValueSelection(int value)
    {
        _value = value;
    }

    public ValueSelection(bool value)
    {
        _value = value ? 1 : 0;
    }

    public int ValueAsInt()
    {
        return _value;
    }

    public bool ValueAsBool()
    {
        return _value != 0;
    }
}
