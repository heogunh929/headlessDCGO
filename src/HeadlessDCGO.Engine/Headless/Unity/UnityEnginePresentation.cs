// ============================================================================================================
// AS-IS PRESENTATION TYPES in the `UnityEngine` namespace: rendering, audio, animation, assets.
//
// WHY THIS FILE EXISTS. The AS-IS sources hold these as fields and pass them around. They live in a compiled
// Unity assembly, so the AS-IS HOME is the namespace, not a file.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. The headless process draws nothing, plays nothing and
// animates nothing. Measured usage is entirely display: none of these appears in a condition that the game
// rules branch on (see docs/symbol_classification.md for how that was established — the rule layer's uses of
// this group are field declarations and assignments only, with zero condition sites).
//
// The one member that is more than a placeholder is `RectTransform`, which derives from `Transform` exactly as
// in Unity so that a `RectTransform` field still participates in the real hierarchy declared in
// Headless/Unity/UnityEngineObjectModel.cs.
//
// Add a type here only when an AS-IS file cannot compile without it.
// ============================================================================================================

namespace UnityEngine;

using System;
using System.Collections.Generic;

/// <summary>Unity <c>RectTransform</c>. Real hierarchy (inherited); its rect fields are held, not computed.</summary>
public class RectTransform : Transform
{
    public Vector2 anchoredPosition { get; set; }
    public Vector2 anchorMin { get; set; }
    public Vector2 anchorMax { get; set; }
    public Vector2 pivot { get; set; }
    public Vector2 sizeDelta { get; set; }
    public Vector2 offsetMin { get; set; }
    public Vector2 offsetMax { get; set; }
    public Rect rect { get; set; }

    /// <summary>Unity <c>RectTransform.GetWorldCorners</c>. Nothing has world geometry; the array is zeroed.</summary>
    public void GetWorldCorners(Vector3[] fourCornersArray)
    {
        for (int i = 0; i < fourCornersArray.Length; i++)
        {
            fourCornersArray[i] = Vector3.zero;
        }
    }
}

/// <summary>Unity <c>Rect</c>. A held value.</summary>
public struct Rect(float x, float y, float width, float height)
{
    public float x = x;
    public float y = y;
    public float width = width;
    public float height = height;

    public readonly float xMin => x;
    public readonly float yMin => y;
    public readonly float xMax => x + width;
    public readonly float yMax => y + height;
    public readonly Vector2 size => new(width, height);
    public readonly Vector2 center => new(x + (width / 2f), y + (height / 2f));

    public static bool operator ==(Rect a, Rect b) => a.Equals(b);

    public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

    public readonly bool Equals(Rect other)
        => x == other.x && y == other.y && width == other.width && height == other.height;

    public readonly override bool Equals(object? obj) => obj is Rect other && Equals(other);

    public readonly override int GetHashCode() => System.HashCode.Combine(x, y, width, height);

    public readonly bool Contains(Vector2 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;

    public readonly bool Contains(Vector3 point) => Contains((Vector2)point);
}

/// <summary>Unity <c>ScriptableObject</c>. A data asset base; nothing loads assets here.</summary>
public class ScriptableObject : Object
{
}

/// <summary>Unity <c>TextAsset</c>. Holds text; nothing loads assets here.</summary>
public sealed class TextAsset : Object
{
    public string text { get; set; } = string.Empty;
    public byte[] bytes { get; set; } = Array.Empty<byte>();

    public override string ToString() => text;
}

/// <summary>Unity <c>Texture</c> / <c>Texture2D</c>. Nothing renders.</summary>
public class Texture : Object
{
    public int width { get; set; }
    public int height { get; set; }
}

public sealed class Texture2D : Texture
{
    public Texture2D()
    {
    }

    /// <summary>Unity <c>Texture2D.LoadImage</c>. Nothing decodes.</summary>
    public bool LoadImage(byte[] data) => false;

    public Texture2D(int width, int height)
    {
        this.width = width;
        this.height = height;
    }
}

/// <summary>Unity <c>Sprite</c>. Nothing renders.</summary>
public sealed class Sprite : Object
{
    public Texture2D? texture { get; set; }
    public Rect rect { get; set; }

    /// <summary>Unity <c>Sprite.Create</c>. Nothing renders; the sprite carries its arguments.</summary>
    public static Sprite Create(Texture2D? texture, Rect rect, Vector2 pivot)
        => new() { texture = texture, rect = rect };

    public static Sprite Create(Texture2D? texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        => Create(texture, rect, pivot);
}

/// <summary>Unity <c>Material</c>. Nothing renders.</summary>
public class Material : Object
{
    public Material()
    {
    }

    public Material(Material? source)
    {
    }

    public Material(Shader? shader)
    {
    }

    public Color color { get; set; } = Color.white;
}

/// <summary>Unity <c>Shader</c>. Nothing renders.</summary>
public sealed class Shader : Object
{
}

/// <summary>Unity <c>Font</c>. Nothing renders text.</summary>
public sealed class Font : Object
{
}

/// <summary>Unity <c>Renderer</c> family. Nothing renders.</summary>
public class Renderer : Component
{
    public Material? material { get; set; }
    public Material? sharedMaterial { get; set; }
    public bool enabled { get; set; } = true;
    public int sortingOrder { get; set; }
    public string sortingLayerName { get; set; } = string.Empty;
}

public sealed class MeshRenderer : Renderer
{
}

public sealed class SpriteRenderer : Renderer
{
    public Sprite? sprite { get; set; }
    public Color color { get; set; } = Color.white;
    public bool flipX { get; set; }
    public bool flipY { get; set; }
}

public sealed class LineRenderer : Renderer
{
    public int positionCount { get; set; }
    public float startWidth { get; set; }
    public float endWidth { get; set; }

    public void SetPosition(int index, Vector3 position)
    {
    }

    public Vector3 GetPosition(int index) => Vector3.zero;

    public float widthMultiplier { get; set; } = 1f;
    public AnimationCurve? widthCurve { get; set; }

    public void SetPositions(Vector3[] positions)
    {
    }
}

/// <summary>Unity <c>Collider</c> family. There is no physics.</summary>
public class Collider : Component
{
    public bool enabled { get; set; } = true;
}

public sealed class MeshCollider : Collider
{
}

/// <summary>Unity <c>Rigidbody</c>. There is no physics.</summary>
public sealed class Rigidbody : Component
{
    public Vector3 velocity { get; set; }
    public bool isKinematic { get; set; }
    public bool useGravity { get; set; } = true;

    public void AddForce(Vector3 force)
    {
    }

    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius)
    {
    }
}

/// <summary>Unity <c>ParticleSystem</c>. Nothing renders.</summary>
public sealed class ParticleSystem : Component
{
    public bool isPlaying { get; private set; }

    public void Play() => isPlaying = true;

    public void Stop() => isPlaying = false;

    public void Clear()
    {
    }
}

/// <summary>Unity <c>AudioClip</c> / <c>AudioSource</c>. Nothing plays.</summary>
public sealed class AudioClip : Object
{
    public float length { get; set; }
}

public sealed class AudioSource : Behaviour
{
    public AudioClip? clip { get; set; }
    public float volume { get; set; } = 1f;
    public float pitch { get; set; } = 1f;
    public bool loop { get; set; }
    public bool isPlaying { get; private set; }

    public void Play() => isPlaying = true;

    public void Stop() => isPlaying = false;

    public void PlayOneShot(AudioClip? clip)
    {
    }

    public void PlayOneShot(AudioClip? clip, float volumeScale)
    {
    }
}

/// <summary>Unity <c>Animator</c> / <c>RuntimeAnimatorController</c>. Nothing animates.</summary>
public sealed class Animator : Behaviour
{
    public RuntimeAnimatorController? runtimeAnimatorController { get; set; }
    public float speed { get; set; } = 1f;

    public void Play(string stateName)
    {
    }

    public void SetTrigger(string name)
    {
    }

    public void SetBool(string name, bool value)
    {
    }

    public void SetInteger(string name, int value)
    {
    }

    public void SetFloat(string name, float value)
    {
    }

    public int GetInteger(string name) => 0;

    public bool GetBool(string name) => false;

    public float GetFloat(string name) => 0f;
}

public class RuntimeAnimatorController : Object
{
}

/// <summary>Unity <c>Camera</c>. Nothing renders.</summary>
public sealed class Camera : Behaviour
{
    public static Camera? main => null;

    public float orthographicSize { get; set; }
    public bool orthographic { get; set; }

    public Vector3 WorldToScreenPoint(Vector3 position) => Vector3.zero;

    public Vector3 ScreenToWorldPoint(Vector3 position) => Vector3.zero;
}

/// <summary>Unity <c>Canvas</c> / <c>CanvasGroup</c>. Nothing renders.</summary>
public sealed class Canvas : Behaviour
{
    public RenderMode renderMode { get; set; }
    public Camera? worldCamera { get; set; }
    public int sortingOrder { get; set; }
    public bool overrideSorting { get; set; }
}

public enum RenderMode
{
    ScreenSpaceOverlay = 0,
    ScreenSpaceCamera = 1,
    WorldSpace = 2,
}

public sealed class CanvasGroup : Behaviour
{
    public float alpha { get; set; } = 1f;
    public bool interactable { get; set; } = true;
    public bool blocksRaycasts { get; set; } = true;
    public bool ignoreParentGroups { get; set; }
}

/// <summary>Unity <c>LayerMask</c>. There is no layer system.</summary>
public struct LayerMask
{
    public int value;

    public static int NameToLayer(string layerName) => 0;

    public static string LayerToName(int layer) => string.Empty;

    public static int GetMask(params string[] layerNames) => 0;

    public static implicit operator int(LayerMask mask) => mask.value;

    public static implicit operator LayerMask(int intVal) => new() { value = intVal };
}

/// <summary>Unity <c>Debug</c>. Logging only — the AS-IS sources use it for diagnostics, never for a decision.
/// Forwarded to the console so those diagnostics remain visible.</summary>
public static class Debug
{
    public static void Log(object? message) => Console.WriteLine(message);

    public static void LogWarning(object? message) => Console.WriteLine($"warning: {message}");

    public static void LogWarning(object? message, Object? context) => LogWarning(message);

    public static void Log(object? message, Object? context) => Log(message);

    public static void LogError(object? message, Object? context) => LogError(message);

    public static void LogError(object? message) => Console.Error.WriteLine($"error: {message}");

    public static void LogException(Exception exception) => Console.Error.WriteLine(exception);

    public static void Assert(bool condition)
    {
    }
}
