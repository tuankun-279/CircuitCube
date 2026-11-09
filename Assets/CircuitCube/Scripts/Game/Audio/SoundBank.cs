using System;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>Every sound the game plays. The order is the order of <see cref="SoundBank.Sounds"/>.</summary>
    public enum Sfx { Rotate, Lamp, Win, Star, Unlock, Hint, Undo, Blocked, Button, PanelOpen, PanelClose }

    [Serializable]
    public sealed class SfxEntry
    {
        public Sfx Id;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("Random pitch change each play (0.05 = ±5%), so a sound heard many times in a row does not tire.")]
        [Range(0f, 0.2f)] public float PitchJitter;
        [Tooltip("Plays closer together than this are dropped (seconds).")]
        public float MinGap = 0.03f;
    }

    /// <summary>
    /// Clips and levels for <see cref="GameAudio"/>. The clips come from ArtRef/audio (Kenney CC0 sound packs, CC0 music
    /// from OpenGameArt); Tools/Circuit Cube/Audio/Build Audio fills this asset. Levels are set here, not in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Circuit Cube/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        public SfxEntry[] Sounds = new SfxEntry[0];

        [Header("Music")]
        [Tooltip("Home, Shapes and Levels screens: the 41 s Outer Space loop.")]
        public AudioClip MenuMusic;
        [Tooltip("While playing a level.")]
        public AudioClip PlayMusic;
        [Range(0f, 1f)] public float MenuVolume = 0.62f;
        [Range(0f, 1f)] public float PlayVolume = 0.35f;
        [Tooltip("Cross-fade between the two tracks (seconds).")]
        public float FadeSeconds = 1.2f;
        [Tooltip("Music level while the win sound and the stars play (share of the normal level).")]
        [Range(0f, 1f)] public float WinDuck = 0.35f;

        [Header("Lamps")]
        [Tooltip("Pitch of the lamp sound per lamp colour 1-7 (red, blue, purple, yellow, orange, green, white): a C major scale.")]
        public float[] LampPitch = { 1f, 1.122f, 1.335f, 1.26f, 1.498f, 1.682f, 2f };

        public SfxEntry Get(Sfx id)
        {
            foreach (var e in Sounds) if (e != null && e.Id == id) return e;
            return null;
        }
    }
}
