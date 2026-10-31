using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using UnityEngine;

namespace Lazpoly.Presentation.Audio
{
    /// <summary>
    /// Plays the calm background tracks one after another, cross-fading between them, for the
    /// whole run. Its volume is the music setting; the master volume sits above it on the listener.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Resource names of the tracks, in playing order.</summary>
        public static readonly string[] Tracks = { "kiyi", "aksam" };

        public const float CrossfadeSeconds = 4f;

        private static MusicPlayer _instance;
        private static bool _quitting;

        private readonly AudioSource[] _sources = new AudioSource[2];
        private AudioClip[] _clips;
        private int _track = -1;
        private int _active;
        private float _fade = 1f;
        private float _volume = GameSettings.Default.MusicVolume;
        private SettingsService _settings;

        public static string ResourcePath(string track) => "Lazpoly/Music/" + track;

        /// <summary>The track playing now, for tests; null before the first one starts.</summary>
        public string Playing => _track < 0 ? null : Tracks[_track];

        public static MusicPlayer Ensure()
        {
            if (_instance != null || _quitting) return _instance;
            var host = new GameObject("Lazpoly Music");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<MusicPlayer>();
            return _instance;
        }

        private void Awake()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.priority = 0;
                _sources[i] = source;
            }

            _clips = new AudioClip[Tracks.Length];
            for (int i = 0; i < Tracks.Length; i++)
            {
                _clips[i] = Resources.Load<AudioClip>(ResourcePath(Tracks[i]));
                if (_clips[i] == null) Debug.LogWarning($"[Lazpoly] Music '{ResourcePath(Tracks[i])}' is missing; run Tools/Audio/make_music.py.");
            }

            Application.quitting += OnQuitting;
            GameServices.Registry?.TryGet(out _settings);
            if (_settings != null)
            {
                _volume = _settings.Current.MusicVolume;
                _settings.Changed += OnSettingsChanged;
            }

            StartNext();
        }

        private void OnDestroy()
        {
            Application.quitting -= OnQuitting;
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            if (_instance == this) _instance = null;
        }

        private static void OnQuitting() => _quitting = true;

        private void OnSettingsChanged(GameSettings settings) => _volume = settings.MusicVolume;

        private void Update()
        {
            AudioSource current = _sources[_active];
            if (current.clip == null) return;

            // Start the next track while this one plays its last seconds, then fade across.
            float left = current.clip.length - current.time;
            if (current.isPlaying && left <= CrossfadeSeconds && _fade >= 1f) StartNext();

            if (_fade < 1f) _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / CrossfadeSeconds);
            AudioSource previous = _sources[1 - _active];
            _sources[_active].volume = _volume * _fade;
            previous.volume = _volume * (1f - _fade);
            if (_fade >= 1f && previous.isPlaying) previous.Stop();
        }

        private void StartNext()
        {
            if (_clips == null || _clips.Length == 0) return;
            int tries = 0;
            do
            {
                _track = (_track + 1) % Tracks.Length;
                tries++;
            } while (_clips[_track] == null && tries < Tracks.Length);
            if (_clips[_track] == null) return;

            _active = _sources[_active].isPlaying ? 1 - _active : _active;
            AudioSource source = _sources[_active];
            source.clip = _clips[_track];
            source.volume = 0f;
            source.Play();
            _fade = _sources[1 - _active].isPlaying ? 0f : 1f;
        }
    }
}
