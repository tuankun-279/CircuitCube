using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>One place for every colour. Energy colours are for wires, sources and lamps only.</summary>
    public static class CubeColors
    {
        static Color Hex(uint rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public static readonly Color Background = Hex(0x1A1F33);
        public static readonly Color BackgroundDeep = Hex(0x12162A);
        public static readonly Color CubeCore = Hex(0x0E1220);
        public static readonly Color TileBase = Hex(0x2A3150);
        public static readonly Color WireOff = Hex(0x7A6A55);
        public static readonly Color LampOff = Hex(0x232A3A);
        public static readonly Color Hinted = Hex(0xFFB547);

        // UI
        public static readonly Color Panel = Hex(0xFFF8EE);
        public static readonly Color Ink = Hex(0x1F2340);
        public static readonly Color InkSoft = Hex(0x5A607A);
        public static readonly Color Accent = Hex(0xFFB547);
        public static readonly Color OnDark = Hex(0xEEF1F7);
        public static readonly Color OnDarkSoft = Hex(0x9AA3B6);
        public static readonly Color Locked = Hex(0x3A4160);

        /// <summary>Unpowered wire colour of the current theme.</summary>
        public static Color WireOffColor => Themes.Current != null ? Themes.Current.WireOff : WireOff;

        public static Color Energy(int set)
        {
            if (Themes.Current != null) return Themes.Current.Energy(set);
            switch (set)
            {
                case 1: return Hex(0xFF5B4E);   // red
                case 2: return Hex(0x4FA3FF);   // blue
                case 4: return Hex(0xFFC928);   // yellow
                case 3: return Hex(0xB072FF);   // purple
                case 5: return Hex(0xFF9433);   // orange
                case 6: return Hex(0x43D88A);   // green
                case 7: return Hex(0xF4F4F4);   // white
                default: return WireOff;
            }
        }

        public static string EnergyName(int set)
        {
            switch (set)
            {
                case 1: return "Red";
                case 2: return "Blue";
                case 4: return "Yellow";
                case 3: return "Purple";
                case 5: return "Orange";
                case 6: return "Green";
                case 7: return "White";
                default: return "None";
            }
        }
    }
}
