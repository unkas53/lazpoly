using System.Collections.Generic;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Audio
{
    /// <summary>
    /// Plays the game's sound effects through a small pool of 2D audio sources. One instance lives
    /// for the whole run (created on first use) and also gives every interface button its click.
    /// The master volume is <see cref="AudioListener.volume"/>, set by the settings service; the
    /// effects volume from the same settings scales every cue under it.
    /// </summary>
    public sealed class AudioPlayer : MonoBehaviour
    {
        private const int Voices = 10;

        /// <summary>The same cue again within this time is skipped, so bursts of events do not stack up.</summary>
        private const float RepeatGapSeconds = 0.045f;

        private static AudioPlayer _instance;
        private static bool _quitting;

        private readonly AudioSource[] _sources = new AudioSource[Voices];
        private readonly Dictionary<SoundCue, AudioClip[]> _clips = new Dictionary<SoundCue, AudioClip[]>();
        private readonly Dictionary<SoundCue, int> _lastVariant = new Dictionary<SoundCue, int>();
        private readonly Dictionary<SoundCue, float> _lastPlayed = new Dictionary<SoundCue, float>();
        private readonly HashSet<SoundCue> _missing = new HashSet<SoundCue>();
        private int _next;
        private float _effectsVolume = GameSettings.Default.EffectsVolume;
        private SettingsService _settings;

        /// <summary>How many sounds have started since the game launched; tests use it to see that a cue played.</summary>
        public static int PlayedCount { get; private set; }

        /// <summary>The cue that played last, for tests.</summary>
        public static SoundCue LastCue { get; private set; }

        /// <summary>Creates the player if needed. Called by each scene's root so sounds work from the first frame.</summary>
        public static AudioPlayer Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("Lazpoly Audio");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AudioPlayer>();
            return _instance;
        }

        /// <summary>Plays a cue; does nothing for <see cref="SoundCue.None"/>.</summary>
        public static void Play(SoundCue cue)
        {
            // Nothing may be created while the game shuts down.
            if (_quitting || cue == SoundCue.None) return;
            Ensure().PlayCue(cue);
        }

        private void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }

            UiBuilder.Pressed += OnUiPressed;
            UiFadeIn.Shown += OnWindowShown;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnQuitting;
            EnsureListener();

            GameServices.Registry?.TryGet(out _settings);
            if (_settings != null)
            {
                _effectsVolume = _settings.Current.EffectsVolume;
                _settings.Changed += OnSettingsChanged;
            }
        }

        private void OnSettingsChanged(GameSettings settings) => _effectsVolume = settings.EffectsVolume;

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            UiBuilder.Pressed -= OnUiPressed;
            UiFadeIn.Shown -= OnWindowShown;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting -= OnQuitting;
            if (_instance == this) _instance = null;
        }

        private static void OnUiPressed() => Play(SoundCue.UiClick);

        private static void OnQuitting() => _quitting = true;

        private static void OnWindowShown() => Play(SoundCue.UiOpen);

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureListener();

        /// <summary>Sounds need one listener in the scene; a camera built in code may not have one.</summary>
        private static void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            Camera camera = Camera.main;
            if (camera != null) camera.gameObject.AddComponent<AudioListener>();
        }

        private void PlayCue(SoundCue cue)
        {
            if (cue == SoundCue.None) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < RepeatGapSeconds) return;

            AudioClip[] takes = ClipsFor(cue);
            if (takes == null) return;
            _lastPlayed[cue] = now;
            int last = _lastVariant.TryGetValue(cue, out int previous) ? previous : -1;
            int variant = SoundCues.PickVariant(takes.Length, last, Random.value);
            _lastVariant[cue] = variant;
            AudioClip clip = takes[variant];

            AudioSource source = FreeSource();
            float jitter = SoundCues.PitchJitter(cue);
            source.pitch = 1f + Random.Range(-jitter, jitter);
            source.volume = SoundCues.Volume(cue) * _effectsVolume;
            source.clip = clip;
            source.Play();
            PlayedCount++;
            LastCue = cue;
        }

        // A source that is not playing, or else the one started longest ago.
        private AudioSource FreeSource()
        {
            for (int i = 0; i < Voices; i++)
            {
                AudioSource candidate = _sources[(_next + i) % Voices];
                if (candidate.isPlaying) continue;
                _next = (_next + i + 1) % Voices;
                return candidate;
            }

            AudioSource oldest = _sources[_next];
            _next = (_next + 1) % Voices;
            return oldest;
        }

        /// <summary>Every take of a cue that is in the build (the first one must be); null when the cue has no clip.</summary>
        private AudioClip[] ClipsFor(SoundCue cue)
        {
            if (_clips.TryGetValue(cue, out AudioClip[] takes)) return takes;
            if (_missing.Contains(cue)) return null;

            AudioClip first = Resources.Load<AudioClip>(SoundCues.ResourcePath(cue));
            if (first == null)
            {
                _missing.Add(cue);
                Debug.LogWarning($"[Lazpoly] Sound '{SoundCues.ResourcePath(cue)}' is missing; run Tools/Audio/make_sfx.py.");
                return null;
            }

            var found = new List<AudioClip> { first };
            for (int i = 1; i < SoundCues.Variants(cue); i++)
            {
                AudioClip take = Resources.Load<AudioClip>(SoundCues.VariantPath(cue, i));
                if (take != null) found.Add(take);
            }

            takes = found.ToArray();
            _clips[cue] = takes;
            return takes;
        }
    }
}
