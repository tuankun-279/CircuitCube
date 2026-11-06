using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// The UI canvas prefab (Prefabs/UI/UI.prefab). Holds the screens and the pop-up cards; all layout, sprites and
    /// animations are set in the prefab. The game only fills in text and listens to buttons.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public HomeScreen Home;
        public ShapesScreen Shapes;
        public LevelsScreen Levels;
        public HudScreen Hud;
        public GameObject Dim;        // shown under any open card
        public Card Result, Tip;
    }
}
