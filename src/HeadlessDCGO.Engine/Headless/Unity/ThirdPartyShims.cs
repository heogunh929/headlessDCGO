// ============================================================================================================
// AS-IS THIRD-PARTY NAMESPACES: DOTween (`DG.Tweening`), Cinemachine, Coffee/UIEffect, Shapes2D, UIShiny, WebP,
// WebSocketSharp, Photon.Realtime, and the assorted `UnityEngine.*` sub-namespaces the sources open with
// `using` but barely touch.
//
// WHY THIS FILE EXISTS. The AS-IS sources open these namespaces at the top of many files and use a handful of
// members. They live in compiled third-party assemblies, so the AS-IS HOME is the namespace, not a file.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. DOTween animates, Cinemachine moves cameras, UIEffect draws
// shine — none of that exists here. The tween extension methods return an inert handle so that
// `transform.DOScale(...).SetEase(...)` chains still compile and evaluate to something.
//
// WHEN A TWEEN COMPLETES, AND WHY IT MATTERS. `Play()` SCHEDULES; the tween completes on the NEXT TICK, and
// getting this wrong breaks the engine in two opposite ways.
//
//   Too late (never)     The AS-IS pacing idiom is
//                            sequence.AppendCallback(() => end = true); sequence.Play();
//                            yield return new WaitWhile(() => !end);
//                        33 such sites in `Effects.cs`. An inert tween leaves `end` false forever.
//
//   Too early (on Play)  `Effects.CreateFieldPermanentCardEffect` drops a card from z = -30 to z = 0 and then
//                        writes
//                            sequence.Play();
//                            while (Mathf.Abs(localPosition.z - (-0.2f)) < 1) yield return null;
//                        In Unity that check runs BEFORE the tween has moved anything: z is still -30,
//                        |-30 + 0.2| = 29.8, the condition is false and the loop is SKIPPED. Complete on
//                        Play() instead and z is already 0, |0 + 0.2| = 0.2 < 1, and the loop never exits.
//                        (This shim did exactly that, and it looked like an AS-IS bug until measured.)
//
// So completion is deferred by one tick: creation enqueues, and the coroutine driver drains the queue at each
// tick boundary — the closest thing here to Unity's "the tween updates on the next frame". The animation's
// DURATION still disappears; only its ordering relative to the caller's next statement is preserved, and that
// ordering is what the AS-IS code reads.
//
// AUTOPLAY. DOTween starts a tween as soon as it is created; `Play()` only matters after a `Pause()`. The
// AS-IS code relies on that — of 52 `DOTween.Sequence()` sites, 17 never call `Play()` and simply wait
// (`CardObjectController.AddHandCard` builds its draw animation and goes straight to
// `yield return new WaitWhile(() => !end)`). Requiring an explicit Play here left those waiting forever, so a
// tween schedules itself on creation.
// ============================================================================================================

namespace DG.Tweening
{

    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>DOTween <c>Ease</c>. Carried so ease arguments compile.</summary>
    public enum Ease
    {
        Unset = 0,
        Linear = 1,
        InSine = 2,
        OutSine = 3,
        InOutSine = 4,
        InQuad = 5,
        OutQuad = 6,
        InOutQuad = 7,
        InCubic = 8,
        OutCubic = 9,
        InOutCubic = 10,
        InBack = 11,
        OutBack = 12,
        InOutBack = 13,
        InBounce = 14,
        OutBounce = 15,
        InOutBounce = 16,
        InElastic = 17,
        OutElastic = 18,
        InOutElastic = 19,
        Flash = 20,
    }

    public enum LoopType
    {
        Restart = 0,
        Yoyo = 1,
        Incremental = 2,
    }

    /// <summary>DOTween <c>Tween</c> — an inert handle. Nothing interpolates; see the file header for what
    /// <see cref="Play"/> does about completion callbacks.</summary>
    public class Tween
    {
        private readonly System.Collections.Generic.List<Action> _callbacks = new();

        /// <summary>DOTween autoplays: a tween runs from the moment it exists. See the file header.</summary>
        public Tween() => Pending.Enqueue(this);

        public bool IsPlaying { get; private set; } = true;

        public Tween SetEase(Ease ease) => this;

        public Tween SetEase(AnimationCurve curve) => this;

        public Tween SetLoops(int loops) => this;

        public Tween SetLoops(int loops, LoopType loopType) => this;

        public Tween SetDelay(float delay) => this;

        public Tween SetUpdate(bool isIndependentUpdate) => this;

        public Tween SetAutoKill(bool autoKillOnCompletion) => this;

        public Tween SetRelative(bool isRelative = true) => this;

        public Tween From() => this;

        public Tween OnComplete(Action action)
        {
            _callbacks.Add(action);

            return this;
        }

        public Tween OnStart(Action action) => this;

        public Tween OnUpdate(Action action) => this;

        public Tween OnKill(Action action)
        {
            _callbacks.Add(action);

            return this;
        }

        /// <summary>Schedules the tween. It completes on the NEXT tick, not now — see the file header for the
        /// AS-IS loop that depends on the value being UNCHANGED on the statement after Play().</summary>
        public virtual Tween Play()
        {
            if (!IsPlaying)
            {
                IsPlaying = true;
                Pending.Enqueue(this);
            }

            return this;
        }

        /// <summary>Tweens waiting for their tick. Drained by
        /// <see cref="HeadlessDCGO.Engine.Headless.Coroutines.CoroutineDriver"/>.</summary>
        internal static readonly Queue<Tween> Pending = new();

        /// <summary>Completes every scheduled tween. Called once per tick by the driver.</summary>
        public static void AdvanceScheduled()
        {
            while (Pending.Count > 0)
            {
                Pending.Dequeue().Complete();
            }
        }

        public Tween Pause()
        {
            IsPlaying = false;

            return this;
        }

        public Tween Kill(bool complete = false)
        {
            if (complete)
            {
                Complete();
            }

            return this;
        }

        public Tween Complete()
        {
            IsPlaying = false;

            foreach (Action callback in _callbacks.ToArray())
            {
                callback();
            }

            _callbacks.Clear();

            return this;
        }
    }

    /// <summary>DOTween <c>Tweener</c>.</summary>
    public class Tweener : Tween
    {
    }

    /// <summary>DOTween <c>Sequence</c>. Appended items are not scheduled; callbacks run on
    /// <see cref="Tween.Play"/> in the order they were appended.</summary>
    public sealed class Sequence : Tween
    {
        /// <summary>Appended tweens are not scheduled, but their completion callbacks must still run: the
        /// AS-IS pacing idiom is `sequence.Append(x).AppendCallback(() => end = true); sequence.Play();
        /// yield return new WaitWhile(() => !end);` and a swallowed callback hangs that wait forever
        /// (`LoadingObject.EndLoading`, LoadingObject.cs:104-110). Chaining them here keeps Play() releasing
        /// every waiter.</summary>
        public Sequence Append(Tween tween)
        {
            OnComplete(() => tween.Complete());

            return this;
        }

        public Sequence Join(Tween tween)
        {
            OnComplete(() => tween.Complete());

            return this;
        }

        public Sequence Insert(float atPosition, Tween tween) => this;

        public Sequence AppendInterval(float interval) => this;

        public Sequence AppendCallback(TweenCallback callback)
        {
            OnComplete(() => callback());

            return this;
        }

        public Sequence PrependInterval(float interval) => this;
    }

    public delegate void TweenCallback();

    /// <summary>DOTween entry point.</summary>
    public static class DOTween
    {
        public static Sequence Sequence() => new();

        /// <summary>DOTween <c>To</c> — interpolates a value over time. The interpolation is skipped, but the
        /// SETTER STILL RUNS WITH THE END VALUE, because the AS-IS code waits on the RESULT, not on the tween:
        ///     sequence.Append(DOTween.To(() =&gt; handCard.transform.localScale, x =&gt; … = x, Vector3.zero, .22f));
        ///     while (handCard != null) { if (handCard.transform.localScale.x &gt; 0.2f) yield return null; else break; }
        /// (Effects.cs:113-135). A tween that never assigns leaves the scale at 1 and that loop never exits.
        /// Jumping to the end value is the headless equivalent of the animation having finished.</summary>
        public static Tween To(Func<float> getter, Action<float> setter, float endValue, float duration)
            => Assign(setter, endValue);

        public static Tween To(Func<Vector3> getter, Action<Vector3> setter, Vector3 endValue, float duration)
            => Assign(setter, endValue);

        public static Tween To(Func<Vector2> getter, Action<Vector2> setter, Vector2 endValue, float duration)
            => Assign(setter, endValue);

        public static Tween To(Func<Color> getter, Action<Color> setter, Color endValue, float duration)
            => Assign(setter, endValue);

        /// <summary>The assignment happens when the tween is COMPLETED, not when it is created — a tween that
        /// is built but never played must not change anything, as in DOTween.</summary>
        private static Tween Assign<T>(Action<T> setter, T endValue)
        {
            Tween tween = new();
            tween.OnComplete(() => setter(endValue));

            return tween;
        }

        public static void Kill(object target, bool complete = false)
        {
        }

        public static void KillAll(bool complete = false)
        {
        }
    }

    /// <summary>DOTween's transform/graphic extension methods. Each returns an inert handle.</summary>
    public static class ShortcutExtensions
    {
        public static Tweener DOMove(this Transform target, Vector3 endValue, float duration) => new();

        public static Tweener DOLocalMove(this Transform target, Vector3 endValue, float duration) => new();

        public static Tweener DOLocalMoveX(this Transform target, float endValue, float duration) => new();

        public static Tweener DOLocalMoveY(this Transform target, float endValue, float duration) => new();

        public static Tweener DOLocalMoveZ(this Transform target, float endValue, float duration) => new();

        public static Tweener DOMoveX(this Transform target, float endValue, float duration) => new();

        public static Tweener DOMoveY(this Transform target, float endValue, float duration) => new();

        public static Tweener DOScale(this Transform target, Vector3 endValue, float duration) => new();

        public static Tweener DOScale(this Transform target, float endValue, float duration) => new();

        public static Tweener DOScaleX(this Transform target, float endValue, float duration) => new();

        public static Tweener DOScaleY(this Transform target, float endValue, float duration) => new();

        public static Tweener DORotate(this Transform target, Vector3 endValue, float duration) => new();

        public static Tweener DOLocalRotate(
        this Transform target, Vector3 endValue, float duration, RotateMode mode = RotateMode.Fast) => new();

    public static Tweener DORotate(
        this Transform target, Vector3 endValue, float duration, RotateMode mode) => new();

        public static Tweener DOFade(this UnityEngine.UI.Graphic target, float endValue, float duration) => new();

        public static Tweener DOColor(this UnityEngine.UI.Graphic target, Color endValue, float duration) => new();

        public static Tweener DOFade(this SpriteRenderer target, float endValue, float duration) => new();

        public static Tweener DOColor(this SpriteRenderer target, Color endValue, float duration) => new();

        public static Tweener DOFade(this CanvasGroup target, float endValue, float duration) => new();

        public static Tweener DOFade(this AudioSource target, float endValue, float duration) => new();
    }

}

namespace UnityEngine
{

    /// <summary>Unity <c>AnimationCurve</c>. Nothing evaluates curves.</summary>
    public sealed class AnimationCurve
    {
        public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd) => new();

        public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd) => new();

        public float Evaluate(float time) => 0f;

    public int AddKey(float time, float value) => 0;
    }

}

namespace UnityEngine.SceneManagement
{

    /// <summary>Unity scene management. There are no scenes.</summary>
    public struct Scene
    {
        public string name { get; set; }
        public int buildIndex { get; set; }
        public bool IsValid() => false;
    }

    public static class SceneManager
    {
        public static Scene GetActiveScene() => default;

        public static void LoadScene(string sceneName)
        {
        }

        public static void LoadScene(int sceneBuildIndex)
        {
        }

        public static void LoadScene(string sceneName, LoadSceneMode mode)
        {
        }

        public static AsyncOperation LoadSceneAsync(string sceneName) => new();

        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode) => new();

        public static AsyncOperation UnloadSceneAsync(string sceneName) => new();

        public static Scene GetSceneByName(string name) => default;

        public static bool SetActiveScene(Scene scene) => false;
    }

}

namespace UnityEngine.Networking
{

    /// <summary>Unity legacy networking. There is no network.</summary>
    public sealed class UnityWebRequest : System.IDisposable
    {
        public string? error => null;

        public bool isDone => true;

        public bool isNetworkError => false;

        public bool isHttpError => false;

        public DownloadHandler downloadHandler { get; } = new();

        public Result result => Result.Success;

        public enum Result
        {
            InProgress = 0,
            Success = 1,
            ConnectionError = 2,
            ProtocolError = 3,
            DataProcessingError = 4,
        }

        public static UnityWebRequest Get(string uri) => new();

        public static string EscapeURL(string s) => System.Uri.EscapeDataString(s);

        public UnityWebRequestAsyncOperation SendWebRequest() => new();

        public void Dispose()
        {
        }
    }

}

namespace UnityEngine.Rendering
{

    public enum CompareFunction
    {
        Disabled = 0,
        Never = 1,
        Less = 2,
        Equal = 3,
        LessEqual = 4,
        Greater = 5,
        NotEqual = 6,
        GreaterEqual = 7,
        Always = 8,
    }

}

namespace UnityEngine.Pool
{

    using System;
    using System.Collections.Generic;

    /// <summary>Unity object pooling. Allocates rather than pools; nothing depends on reuse.</summary>
    public sealed class ObjectPool<T>(Func<T> createFunc) where T : class
    {
        public T Get() => createFunc();

        public void Release(T element)
        {
        }

        public void Clear()
        {
        }
    }

}

namespace UnityEngine.TextCore
{

    public struct Glyph
    {
    }

}

namespace UnityEngine.UIElements
{

    public class VisualElement
    {
    }

}

namespace UnityEngine.XR
{

    public static class XRSettings
    {
        public static bool enabled => false;
    }

}

namespace UnityEngine.Analytics
{

    public static class Analytics
    {
        public static bool enabled { get; set; }
    }

}

namespace Photon.Realtime
{

    using System.Collections.Generic;

    /// <summary>Photon <c>RoomInfo</c>. There is no network; nothing populates a room list.</summary>
    public class RoomInfo
    {
        public string Name { get; set; } = string.Empty;
        public byte MaxPlayers { get; set; }
        public int PlayerCount { get; set; }
        public bool IsOpen { get; set; }
        public bool IsVisible { get; set; }
        public bool RemovedFromList { get; set; }

        /// <summary>Photon custom properties. Held locally; nothing synchronises.</summary>
        public ExitGames.Client.Photon.Hashtable CustomProperties { get; } = new();
    }

    /// <summary>Photon <c>Player</c> — NOT the game's `Player`. The AS-IS game class of that name lives in
    /// Assets/Scripts/Script/Player.cs; this is the network peer type from the PUN assembly.</summary>
    public class Player
    {
        public int ActorNumber { get; set; }
        public string NickName { get; set; } = string.Empty;
        public bool IsLocal { get; set; }
        public bool IsMasterClient { get; set; }
        public string UserId { get; set; } = string.Empty;

        /// <summary>Photon custom properties. Held locally; nothing synchronises.</summary>
        public ExitGames.Client.Photon.Hashtable CustomProperties { get; } = new();

        public bool SetCustomProperties(
            ExitGames.Client.Photon.Hashtable propertiesToSet,
            ExitGames.Client.Photon.Hashtable? expectedValues = null,
            object? webFlags = null)
        {
            foreach (object key in propertiesToSet.Keys)
            {
                CustomProperties[key] = propertiesToSet[key];
            }

            return true;
        }
    }

    public class RoomOptions
    {
        public byte MaxPlayers { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsOpen { get; set; } = true;
        public bool PublishUserId { get; set; }
        public ExitGames.Client.Photon.Hashtable CustomRoomProperties { get; set; } = new();
        public string[] CustomRoomPropertiesForLobby { get; set; } = System.Array.Empty<string>();
    }

    /// <summary>Photon <c>ClientState</c>. There is no client; the state never leaves disconnected.</summary>
    public enum ClientState
    {
        PeerCreated = 0,
        Disconnected = 1,
        ConnectedToMasterServer = 2,
        JoinedLobby = 3,
        Joined = 4,
    }

    public enum DisconnectCause
    {
        None = 0,
        ExceptionOnConnect = 1,
        Exception = 2,
        DisconnectByServerLogic = 3,
        MaxCcuReached = 4,
        InvalidRegion = 5,
        ServerTimeout = 6,
        ClientTimeout = 7,
        DisconnectByServerReasonUnknown = 8,
        AuthenticationTicketExpired = 9,
    }
}
