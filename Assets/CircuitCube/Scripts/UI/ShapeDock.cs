using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>The row of shapes at the bottom of the weekly page and the leaderboards card.</summary>
    public sealed class ShapeDock : MonoBehaviour
    {
        public ShapeDockItem[] Items = new ShapeDockItem[0];
        [Tooltip("Group icon ids (cube, tetra, ...) and their white line icons (Art/UI/Icons/icon_dock_<id>).")]
        public string[] IconIds = new string[0];
        public Sprite[] Icons = new Sprite[0];

        /// <summary>The line icon of a group icon id; null for a shape that has none yet.</summary>
        public Sprite IconFor(string id)
        {
            int i = System.Array.IndexOf(IconIds, id);
            return i >= 0 && i < Icons.Length ? Icons[i] : null;
        }
    }
}
