using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// The UI canvas prefab (Prefabs/UI/UI.prefab). Holds the screens and the pop-up cards; all layout, sprites and
    /// animations are set in the prefab. The game only fills in text and listens to buttons.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public SplashScreen Splash;
        public HomeScreen Home;
        public ShapesScreen Shapes;
        public LevelsScreen Levels;
        public HudScreen Hud;
        public GameObject Dim;        // shown under any open card
        public Card Result, Tip, Unlock;
        public SettingsCard Settings;
        [Tooltip("Stars that burst out of the solved box and fly into the result card's star row (above every card).")]
        public RectTransform[] FlyingStars = new RectTransform[0];
    }
}
