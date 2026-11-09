using UnityEngine;

namespace CircuitCube.Game
{
    public enum MusicTrack { None, Menu, Play }

    /// <summary>
    /// Plays the game's sounds and music (prefab Prefabs/Audio.prefab, placed in the scene). Two music sources cross-fade
    /// between the menu and level tracks; a small pool of sources plays the effects. The Sound and Music switches in
    /// Settings mute effects and music. Buttons ask for their click through <see cref="Click"/>; it is skipped when the
    /// same press already made a sound (a button that opens a card plays the card's sound only).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public SoundBank Bank;
        public AudioSource MusicA, MusicB;
        public AudioSource[] Effects = new AudioSource[0];

        public static GameAudio Instance { get; private set; }

        MusicTrack _track;
        AudioSource _current, _previous;
        float _duckUntil;
        int _next, _lastPlayFrame = -1;
        bool _clickQueued, _clickBlocked;
        readonly float[] _lastPlay = new float[16];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            foreach (var s in new[] { MusicA, MusicB }) { if (s == null) continue; s.loop = true; s.playOnAwake = false; s.volume = 0f; }
            for (int i = 0; i < _lastPlay.Length; i++) _lastPlay[i] = -1f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static bool SoundOn => CubeSave.Settings.sound;
        static bool MusicOn => CubeSave.Settings.music;

        /// <summary>Play an effect. <paramref name="delay"/> in seconds; <paramref name="pitch"/> multiplies the clip's pitch.</summary>
        public static void Play(Sfx id, float pitch = 1f, float delay = 0f) => Instance?.PlayEffect(id, pitch, delay);

        /// <summary>The lamp chime, pitched by the lamp's colour (1-7).</summary>
        public static void Lamp(int color)
        {
            var i = Instance;
            if (i == null || i.Bank == null || color <= 0) return;
            var p = i.Bank.LampPitch;
            i.PlayEffect(Sfx.Lamp, p != null && color - 1 < p.Length ? p[color - 1] : 1f, 0f);
        }

        /// <summary>A button press: its click (or the blocked sound for a disabled button) unless something else sounded this frame.</summary>
        public static void Click(bool enabled)
        {
            var i = Instance;
            if (i == null) return;
            i._clickQueued = true;
            i._clickBlocked = !enabled;
        }

        public static void Music(MusicTrack track) => Instance?.SetTrack(track);

        /// <summary>Lower the music for a moment (the win fanfare and the stars).</summary>
        public static void Duck(float seconds) { if (Instance != null) Instance._duckUntil = Time.unscaledTime + seconds; }

        void PlayEffect(Sfx id, float pitch, float delay)
        {
            _lastPlayFrame = Time.frameCount;
            if (!SoundOn || Bank == null || Effects.Length == 0) return;
            var e = Bank.Get(id);
            if (e == null || e.Clip == null) return;
            int k = (int)id;
            float now = Time.unscaledTime + delay;
            if (k < _lastPlay.Length && delay <= 0f && _lastPlay[k] >= 0f && now - _lastPlay[k] < e.MinGap) return;
            if (k < _lastPlay.Length) _lastPlay[k] = now;
            var src = NextSource();
            src.clip = e.Clip;
            src.volume = e.Volume;
            src.pitch = pitch * (e.PitchJitter > 0f ? 1f + Random.Range(-e.PitchJitter, e.PitchJitter) : 1f);
            if (delay > 0f) src.PlayScheduled(AudioSettings.dspTime + delay);
            else src.Play();
        }

        AudioSource NextSource()
        {
            for (int n = 0; n < Effects.Length; n++)
            {
                var s = Effects[(_next + n) % Effects.Length];
                if (!s.isPlaying) { _next = (_next + n + 1) % Effects.Length; return s; }
            }
            var oldest = Effects[_next];
            _next = (_next + 1) % Effects.Length;
            return oldest;
        }

        void SetTrack(MusicTrack track)
        {
            if (track == _track || Bank == null) return;
            _track = track;
            var clip = track == MusicTrack.Menu ? Bank.MenuMusic : track == MusicTrack.Play ? Bank.PlayMusic : null;
            _previous = _current;
            _current = _current == MusicA ? MusicB : MusicA;
            if (_current == null) return;
            _current.Stop();
            _current.clip = clip;
            _current.volume = 0f;
            if (clip != null) _current.Play();
        }

        void LateUpdate()
        {
            if (_clickQueued)
            {
                _clickQueued = false;
                if (_lastPlayFrame != Time.frameCount) PlayEffect(_clickBlocked ? Sfx.Blocked : Sfx.Button, 1f, 0f);
            }
            if (Bank == null) return;
            float dt = Time.unscaledDeltaTime, step = Bank.FadeSeconds > 0f ? dt / Bank.FadeSeconds : 1f;
            float level = !MusicOn ? 0f : _track == MusicTrack.Menu ? Bank.MenuVolume : _track == MusicTrack.Play ? Bank.PlayVolume : 0f;
            if (Time.unscaledTime < _duckUntil) level *= Bank.WinDuck;
            if (_current != null) _current.volume = Mathf.MoveTowards(_current.volume, level, step * Mathf.Max(level, 0.3f));
            if (_previous != null && _previous != _current)
            {
                _previous.volume = Mathf.MoveTowards(_previous.volume, 0f, step);
                if (_previous.volume <= 0f && _previous.isPlaying) _previous.Stop();
            }
        }
    }
}
