using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Where the art lives. One place for the editor tools, so moving a folder means changing it here only.
    /// <code>
    /// Art/Board/{Dark,Light}         tile layers per theme (face, wires and cores, lamp and source parts)
    /// Art/Board/Shared               layers both themes use (hint pin)
    /// Art/Materials/Board/...        materials of the board skins (the renderer reads their textures)
    /// Art/UI/Common                  9-slice and plain UI shapes (button, card, circle, pill, square, bar)
    /// Art/UI/Icons                   white icons, tinted by the theme
    /// Art/UI/Shapes/{Dark,Light}     shape group pictures per theme
    /// Art/UI/Brand, Mascot           logo, app icon, robot faces
    /// Art/UI/Backgrounds             full-screen sky per theme
    /// Art/Fonts                      Inter and its TextMeshPro font assets
    /// Art/Atlases                    the UI sprite atlas and the board atlases (generated)
    /// </code>
    /// </summary>
    public static class ArtPaths
    {
        public const string Root = "Assets/CircuitCube/Art";
        public const string Board = Root + "/Board";
        public const string BoardMaterials = Root + "/Materials/Board";
        public const string UI = Root + "/UI";
        public const string Icons = UI + "/Icons";
        public const string Fonts = Root + "/Fonts";
        public const string Atlases = Root + "/Atlases";

        static readonly string[] UiFolders = { "Common", "Brand", "Mascot", "Backgrounds", "Icons" };

        /// <summary>
        /// A UI sprite by name: "ui_card", "mascot_win", "logo_mark", "Backgrounds/bg_dark", or "Dark/shape_cube" for
        /// the shape pictures of a theme.
        /// </summary>
        public static Sprite Sprite(string name)
        {
            int slash = name.IndexOf('/');
            if (slash > 0)
            {
                string head = name.Substring(0, slash), file = name.Substring(slash + 1);
                string folder = head == "Dark" || head == "Light" ? UI + "/Shapes/" + head : UI + "/" + head;
                return AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + file + ".png");
            }
            foreach (var f in UiFolders)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(UI + "/" + f + "/" + name + ".png");
                if (s != null) return s;
            }
            return null;
        }

        public static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "/" + name + ".png");
    }
}
