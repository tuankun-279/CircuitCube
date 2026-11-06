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

    /// <summary>
    /// The board images of one tile shape for one theme, by file name (Art/Board/&lt;theme&gt;/&lt;name&gt;.png). The game
    /// draws them out of the theme's board atlas; wire and core lists follow the tile's wire masks (BoardLook Canon).
    /// </summary>
    [Serializable]
    public sealed class BoardSkin
    {
        public string Base;
        [Tooltip("The face is cut out by its alpha (the triangle tile).")]
        public bool ClipBase;
        public string[] Wires = new string[0];
        public string[] WireCores = new string[0];
        public string LampHalo, LampRing, LampOff, LampOn;
        public string SourceHalo, SourceOutline, SourceCenter;
    }

    /// <summary>One image in a theme's board atlas: its file name and its UV scale (xy) and offset (zw).</summary>
    [Serializable]
    public struct AtlasRect
    {
        public string Image;
        public Vector4 Uv;
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
