// ============================================================================================================
// THE AS-IS VALUE TYPES `UnityEngine.Vector3` / `Vector2` / `Quaternion` / `Color`, AND THE `Mathf` HELPERS.
//
// WHY THIS FILE EXISTS. These live in a compiled Unity assembly, so there is no AS-IS *file* to mirror — the
// AS-IS HOME is the namespace `UnityEngine`. Declaring them here lets the AS-IS sources compile unmodified.
//
// MEMBERS THAT DO REAL WORK
//     Mathf.*                       Plain arithmetic, forwarded to System.Math / System.MathF. Measured uses
//                                   are ordinary numeric work in the rules (`Mathf.Min` in cost/DP maths,
//                                   `Mathf.Abs` in the frame ordering, `Mathf.FloorToInt` in layout).
//                                   These must compute, and computing is all they do.
//     Vector2 / Vector3 arithmetic  Component-wise; carried so expressions in the sources evaluate.
//
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING BEYOND HOLDING A VALUE
//     Vector3 / Vector2 / Quaternion / Color
//                                   Stored and returned. Nothing in the rules branches on them. The one
//                                   rule-adjacent reader is `CardSource.PreferredFrame()`
//                                   (CardSource.cs:2306-2352), which ORDERS empty battle-area frames by
//                                   `localPosition` — a layout choice, not a rule. Every position being equal
//                                   degrades that ordering to list order, which is deterministic; E-01 keeps
//                                   spare slots available so the choice does not bind.
//                                   `Color` is NOT the game's card colour: the AS-IS rules use their own
//                                   `CardColor` enum (CEntity_Base.cs:381, 1478 uses). This type is display.
//     Quaternion.Euler / EulerAngles / FromToRotation
//                                   Return a value that is stored and never read for a decision. They do not
//                                   compute a real rotation.
//
// Add a member here only when an AS-IS file cannot compile without it, and say which list it belongs to.
// ============================================================================================================

namespace UnityEngine;

using System;

/// <summary>Unity <c>UnityEngine.Vector2</c>. A held value; see the file header.</summary>
public struct Vector2(float x, float y) : IEquatable<Vector2>
{
    public float x = x;
    public float y = y;

    public static Vector2 zero => new(0f, 0f);
    public static Vector2 one => new(1f, 1f);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.x * s, a.y * s);
    public static Vector2 operator *(float s, Vector2 a) => a * s;
    public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
    public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);

    public static implicit operator Vector3(Vector2 v) => new(v.x, v.y, 0f);

    public readonly bool Equals(Vector2 other) => x == other.x && y == other.y;
    public readonly override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public readonly override int GetHashCode() => HashCode.Combine(x, y);
    public readonly override string ToString() => $"({x}, {y})";
}

/// <summary>Unity <c>UnityEngine.Vector3</c>. A held value; see the file header.</summary>
public struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
    public float x = x;
    public float y = y;
    public float z = z;

    public Vector3(float x, float y) : this(x, y, 0f)
    {
    }

    public static Vector3 zero => new(0f, 0f, 0f);
    public static Vector3 one => new(1f, 1f, 1f);
    public static Vector3 right => new(1f, 0f, 0f);
    public static Vector3 left => new(-1f, 0f, 0f);
    public static Vector3 up => new(0f, 1f, 0f);
    public static Vector3 down => new(0f, -1f, 0f);
    public static Vector3 forward => new(0f, 0f, 1f);
    public static Vector3 back => new(0f, 0f, -1f);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
    public static Vector3 operator *(float s, Vector3 a) => a * s;
    public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);

    public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);

    public readonly float magnitude => MathF.Sqrt((x * x) + (y * y) + (z * z));
    public readonly float sqrMagnitude => (x * x) + (y * y) + (z * z);
    public readonly Vector3 normalized => magnitude > 0f ? this / magnitude : zero;

    public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + ((b - a) * Mathf.Clamp01(t));

    public readonly bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;
    public readonly override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public readonly override int GetHashCode() => HashCode.Combine(x, y, z);
    public readonly override string ToString() => $"({x}, {y}, {z})";

    public readonly string ToString(string format)
        => $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
}

/// <summary>Unity <c>UnityEngine.Quaternion</c>. A held value; nothing reads it for a decision.</summary>
public struct Quaternion(float x, float y, float z, float w)
{
    public float x = x;
    public float y = y;
    public float z = z;
    public float w = w;

    public static Quaternion identity => new(0f, 0f, 0f, 1f);

    /// <summary>Unity <c>Quaternion.Euler</c>. Carries the angles; does not compute a rotation.</summary>
    public static Quaternion Euler(float xAngle, float yAngle, float zAngle) => new(xAngle, yAngle, zAngle, 1f);

    public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

    public Vector3 eulerAngles
    {
        readonly get => new(x, y, z);
        set { x = value.x; y = value.y; z = value.z; }
    }

    public static Quaternion FromToRotation(Vector3 from, Vector3 to) => identity;

    /// <summary>Unity rotates a vector by a quaternion with <c>*</c>. Nothing rotates here; the vector passes
    /// through unchanged.</summary>
    public static Vector3 operator *(Quaternion rotation, Vector3 point) => point;

    public static Quaternion operator *(Quaternion a, Quaternion b) => a;

    /// <summary>The AS-IS sources call `Quaternion.EulerAngles(new Vector3(...))` — the capitalised spelling
    /// of Euler. Same held value.</summary>
    public static Quaternion EulerAngles(Vector3 euler) => Euler(euler);
}

/// <summary>Unity <c>UnityEngine.Color</c> — DISPLAY colour. The game's card colour is the AS-IS
/// <c>CardColor</c> enum (CEntity_Base.cs:381), not this.</summary>
public struct Color(float r, float g, float b, float a)
{
    public float r = r;
    public float g = g;
    public float b = b;
    public float a = a;

    public Color(float r, float g, float b) : this(r, g, b, 1f)
    {
    }

    public static Color white => new(1f, 1f, 1f);
    public static Color black => new(0f, 0f, 0f);
    public static Color clear => new(0f, 0f, 0f, 0f);
    public static Color red => new(1f, 0f, 0f);
    public static Color green => new(0f, 1f, 0f);
    public static Color blue => new(0f, 0f, 1f);
    public static Color yellow => new(1f, 0.92f, 0.016f);
    public static bool operator ==(Color a, Color b) => a.Equals(b);

    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public readonly bool Equals(Color other) => r == other.r && g == other.g && b == other.b && a == other.a;

    public readonly override bool Equals(object? obj) => obj is Color other && Equals(other);

    public readonly override int GetHashCode() => HashCode.Combine(r, g, b, a);

    public static Color gray => new(0.5f, 0.5f, 0.5f);
    public static Color grey => gray;
}

/// <summary>Unity <c>UnityEngine.Mathf</c>. Real arithmetic — the rules use it for ordinary numeric work.</summary>
public static class Mathf
{
    public const float PI = (float)Math.PI;
    public const float Epsilon = float.Epsilon;
    public const float Infinity = float.PositiveInfinity;
    public const float Deg2Rad = PI / 180f;
    public const float Rad2Deg = 180f / PI;

    public static float Abs(float v) => MathF.Abs(v);
    public static int Abs(int v) => Math.Abs(v);
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static int Min(int a, int b) => Math.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);
    public static float Pow(float f, float p) => MathF.Pow(f, p);
    public static float Sqrt(float v) => MathF.Sqrt(v);
    public static float Sin(float v) => MathF.Sin(v);
    public static float Cos(float v) => MathF.Cos(v);
    public static float Tan(float v) => MathF.Tan(v);
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);
    public static int FloorToInt(float v) => (int)MathF.Floor(v);
    public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
    public static int RoundToInt(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);
    public static float Floor(float v) => MathF.Floor(v);
    public static float Ceil(float v) => MathF.Ceiling(v);
    public static float Round(float v) => MathF.Round(v, MidpointRounding.AwayFromZero);
    public static float Clamp(float v, float lo, float hi) => Math.Clamp(v, lo, hi);
    public static int Clamp(int v, int lo, int hi) => Math.Clamp(v, lo, hi);
    public static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
    public static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));
    public static float Sign(float v) => MathF.Sign(v);
    public static float Log(float f) => MathF.Log(f);
    public static float Log(float f, float p) => MathF.Log(f, p);
    public static float Log10(float f) => MathF.Log10(f);
    public static float Exp(float p) => MathF.Exp(p);
    public static float Repeat(float t, float length) => Clamp(t - (MathF.Floor(t / length) * length), 0f, length);
    public static float MoveTowards(float current, float target, float maxDelta)
        => MathF.Abs(target - current) <= maxDelta ? target : current + (MathF.Sign(target - current) * maxDelta);
    public static bool Approximately(float a, float b) => MathF.Abs(b - a) < 1e-6f;
}
