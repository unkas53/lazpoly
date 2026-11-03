using System.Collections;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Perf;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The game's own opening screen (<see cref="LoadingScreen"/>: paper, the logo, the tagline and a bar) while
    /// the first scene gets ready, held for a moment so it does not flash past, then faded out. It is drawn before
    /// the first scene loads, in place of the Unity splash (turned off in the player settings), and it
    /// never takes the pointer, so nothing underneath waits for it.
    /// </summary>
    public sealed class BootSplash : MonoBehaviour
    {
        /// <summary>How long the screen stays at least, so a fast machine still shows it.</summary>
        public const float HoldSeconds = 1.6f;
        public const float FadeSeconds = 0.5f;
        public const float LogoSize = 420f;

        private static BootSplash _instance;
        private CanvasGroup _group;
        private LoadingScreen _screen;
        private float _started;
        private bool _sceneReady;

        /// <summary>Whether the opening screen is on screen (fading counts).</summary>
        public static bool IsShowing => _instance != null;

        /// <summary>How full the bar is after <paramref name="elapsed"/> seconds: it reaches the end as the hold ends.</summary>
        public static float Progress(float elapsed) => Mathf.Clamp01(elapsed / HoldSeconds);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Show()
        {
            if (PerfRun.Requested || !MenuArt.HasLogo) return; // A measured run goes straight to the table; without the picture there is nothing to show.
            var root = new GameObject("[Boot Splash]", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // Under the scene fader, over every scene canvas.
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _instance = root.AddComponent<BootSplash>();
            _instance._group = root.AddComponent<CanvasGroup>();
            _instance._group.blocksRaycasts = false;
            _instance._group.interactable = false;
            _instance.Build((RectTransform)root.transform);
            _instance._started = Time.realtimeSinceStartup;
            SceneManager.sceneLoaded += _instance.OnSceneLoaded;
            _instance.StartCoroutine(_instance.Run());
        }

        private void Build(RectTransform root) => _screen = LoadingScreen.Build(root, LogoSize, L("Masa kuruluyor..."));

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _sceneReady = true;

        private IEnumerator Run()
        {
            float elapsed;
            do
            {
                elapsed = Time.realtimeSinceStartup - _started;
                _screen.SetProgress(Progress(elapsed));
                yield return null;
            }
            while (elapsed < HoldSeconds || !_sceneReady);

            float fadeStarted = Time.realtimeSinceStartup;
            float t;
            do
            {
                t = Mathf.Clamp01((Time.realtimeSinceStartup - fadeStarted) / FadeSeconds);
                _group.alpha = 1f - Ease.OutCubic(t);
                yield return null;
            }
            while (t < 1f);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
