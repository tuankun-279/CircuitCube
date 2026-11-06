using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>What a UI element is, so the theme can colour it. Set on the element's ThemeRole component.</summary>
    public enum UiRole
    {
        None, Background, Card, Primary, OnPrimary, Secondary, OnSecondary, Round, OnRound,
        Text, TextMuted, Dim, Overlay, Track, Accent, Star, Chip, NodeOpen, NodeCurrent, NodeLocked, OnNode, Transparent
    }

    [Serializable]
    public struct RoleColor
    {
        public UiRole Role;
        public Color Color;
        public RoleColor(UiRole role, Color color) { Role = role; Color = color; }
    }

    /// <summary>Materials of one tile shape for one theme. Wire and core lists follow BoardTile.Wires.</summary>
    [Serializable]
    public sealed class BoardSkin
    {
        public Material Base;
        public Material[] Wires = new Material[0];
        public Material[] WireCores = new Material[0];
        public Material LampHalo, LampRing, LampOff, LampOn;
        public Material SourceHalo, SourceOutline, SourceCenter;
    }

    /// <summary>The theme in use. UI elements with a ThemeRole recolour themselves when it changes.</summary>
    public static class Themes
    {
        public static GameTheme Current { get; private set; }
        public static bool IsDark => Current == null || Current.Dark;
        public static event Action Changed;

        public static void Set(GameTheme theme)
        {
            if (theme == null) return;
            Current = theme;
            Changed?.Invoke();
        }
    }
}
