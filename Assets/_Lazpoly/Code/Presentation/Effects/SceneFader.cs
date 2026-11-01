using System.Collections;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Changes scene behind a short fade to black, so the menu, the lobby and the table do not
    /// jump into each other. One fader lives across scenes; <see cref="Load"/> is the only way in.
    /// Uses unscaled time and takes the pointer while it is dark, so nothing is clicked twice.
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        public const float OutSeconds = 0.18f;
        public const float InSeconds = 0.28f;

        private static SceneFader _instance;
        private CanvasGroup _group;
        private bool _loading;

        /// <summary>True while a scene change is under way; a second request in that time is ignored.</summary>
        public static bool IsLoading => _instance != null && _instance._loading;

        /// <summary>Real time (seconds since start-up) the last scene change was asked for; -1 before the first. The game scene measures its load time from it.</summary>
        public static float LoadRequestedAt { get; private set; } = -1f;

        /// <summary>Fades out, loads <paramref name="sceneName"/> and fades the new scene in.</summary>
        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            LoadRequestedAt = Time.realtimeSinceStartup;
            Ensure().StartCoroutine(Ensure().Run(sceneName));
        }

        private static SceneFader Ensure()
        {
            if (_instance != null) return _instance;
            var root = new GameObject("[Scene Fader]", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            UiRoot.Background(UiRoot.Stretch(root.transform, "Veil"), UiTheme.Backdrop, rounded: false);
            _instance = root.AddComponent<SceneFader>();
            _instance._group = root.AddComponent<CanvasGroup>();
            _instance._group.alpha = 0f;
            _instance._group.blocksRaycasts = false;
            return _instance;
        }

        private IEnumerator Run(string sceneName)
        {
            _loading = true;
            _group.blocksRaycasts = true;
            yield return Fade(0f, 1f, OutSeconds);
            SceneManager.LoadScene(sceneName);
            yield return null; // Let the new scene build its first frame under the veil.
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
