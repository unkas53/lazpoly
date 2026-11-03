using System.Collections;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Changes scene behind the paper loading screen (<see cref="LoadingScreen"/>: the logo and a bar), so going
    /// from the menu to the table and back looks like the game's own opening screen instead of a jump. The
    /// screen fades in, the scene loads in the background while the bar follows it, and the new scene fades in
    /// once it is ready and the screen has been up for <see cref="MinShowSeconds"/> (a fast machine does not
    /// flash it). The new scene starts as soon as it is loaded, so the measured load time is not padded. One
    /// fader lives across scenes; <see cref="Load"/> is the only way in. Uses unscaled time and takes the pointer
    /// while it shows, so nothing is clicked twice.
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        public const float OutSeconds = 0.22f;
        public const float InSeconds = 0.35f;

        /// <summary>The loading screen stays at least this long from the request, fade-in included.</summary>
        public const float MinShowSeconds = 0.9f;

        public const float LogoSize = 300f;

        private static SceneFader _instance;
        private CanvasGroup _group;
        private LoadingScreen _screen;
        private bool _loading;

        /// <summary>True while a scene change is under way; a second request in that time is ignored.</summary>
        public static bool IsLoading => _instance != null && _instance._loading;

        /// <summary>Real time (seconds since start-up) the last scene change was asked for; -1 before the first. The game scene measures its load time from it.</summary>
        public static float LoadRequestedAt { get; private set; } = -1f;

        /// <summary>Fades the loading screen in, loads <paramref name="sceneName"/> and fades the new scene in.</summary>
        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            LoadRequestedAt = Time.realtimeSinceStartup;
            SceneFader fader = Ensure();
            fader._screen.SetCaption(CaptionFor(sceneName));
            fader._screen.SetProgress(0f);
            fader.StartCoroutine(fader.Run(sceneName));
        }

        /// <summary>The line under the bar: where the player is going.</summary>
        public static string CaptionFor(string sceneName) => sceneName == "MainMenu" ? L("Menüye dönülüyor...") : L("Masa kuruluyor...");

        /// <summary>
        /// How full the bar is: it follows the loading (Unity reports 0.9 when a scene is loaded and waits to start it)
        /// but never runs ahead of the time the screen must show, so it fills smoothly even when loading is instant.
        /// </summary>
        public static float Progress(float loadProgress, float shownFor) =>
            Mathf.Min(Mathf.Clamp01(loadProgress / 0.9f), Mathf.Clamp01(shownFor / MinShowSeconds));

        private static SceneFader Ensure()
        {
            if (_instance != null) return _instance;
            var root = new GameObject("[Scene Fader]", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _instance = root.AddComponent<SceneFader>();
            _instance._screen = LoadingScreen.Build((RectTransform)root.transform, LogoSize, CaptionFor("Game"));
            _instance._group = root.AddComponent<CanvasGroup>();
            _instance._group.alpha = 0f;
            _instance._group.blocksRaycasts = false;
            return _instance;
        }

        private IEnumerator Run(string sceneName)
        {
            _loading = true;
            _group.blocksRaycasts = true;
            float started = Time.realtimeSinceStartup;
            yield return Fade(0f, 1f, OutSeconds);

            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
            while (loading != null && !loading.isDone)
            {
                _screen.SetProgress(Progress(loading.progress, Time.realtimeSinceStartup - started));
                yield return null;
            }

            yield return null; // Let the new scene build its first frame under the screen.
            float shown;
            while ((shown = Time.realtimeSinceStartup - started) < MinShowSeconds)
            {
                _screen.SetProgress(Progress(1f, shown));
                yield return null;
            }

            _screen.SetProgress(1f);
            yield return Fade(1f, 0f, InSeconds);
            _group.blocksRaycasts = false;
            _loading = false;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float started = Time.unscaledTime;
            float t;
            do
            {
                t = Mathf.Clamp01((Time.unscaledTime - started) / seconds);
                _group.alpha = Mathf.Lerp(from, to, Ease.OutCubic(t));
                yield return null;
            }
            while (t < 1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            LoadRequestedAt = -1f;
        }
    }
}
