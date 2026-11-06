using CircuitCube.Core;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Debug menu (prefab Prefabs/UI/DebugMenu.prefab). F1 shows or hides it. Only in the editor and development
    /// builds; release builds switch it off. Pick a shape group, type a level, Play; or unlock shapes.
    /// </summary>
    public sealed class DebugMenu : MonoBehaviour
    {
        public CircuitCubeApp App;
        public GameObject Panel;
        public Button[] GroupButtons;      // one per shape group, in group order
        public Image[] GroupMarks;         // highlight behind the selected group button
        public InputField LevelInput;
        public Button PlayButton, UnlockShapeButton, UnlockAllButton, ResetButton, CloseButton;
        public Text Status;

        int _group;

        void Awake()
        {
            if (!Debug.isDebugBuild) { gameObject.SetActive(false); return; }
            for (int i = 0; i < GroupButtons.Length; i++)
            {
                int g = i;
                GroupButtons[i].onClick.AddListener(() => Select(g));
            }
            PlayButton.onClick.AddListener(PlayLevel);
            UnlockShapeButton.onClick.AddListener(UnlockShape);
            UnlockAllButton.onClick.AddListener(UnlockAll);
            ResetButton.onClick.AddListener(ResetAll);
            CloseButton.onClick.AddListener(() => Panel.SetActive(false));
            LevelInput.onSubmit.AddListener(_ => PlayLevel());
            Panel.SetActive(false);
            Select(0);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) Panel.SetActive(!Panel.activeSelf);
            // F2 switches dark / light mode
            if (kb != null && kb.f2Key.wasPressedThisFrame && App != null) App.SetTheme(App.ThemeMode == "light" ? "dark" : "light");
        }

        void Select(int g)
        {
            _group = g;
            for (int i = 0; i < GroupMarks.Length; i++) GroupMarks[i].enabled = i == g;
            if (g >= GameData.GroupCount) { Status.text = "No group " + (g + 1) + " in the data"; return; }
            Status.text = GameData.Group(g).name + ": next level " + CubeSave.Unlocked(g) + " of " + GameData.LevelCount(g) + (CubeSave.GroupOpen(g) ? "" : "  (locked)");
        }

        void PlayLevel()
        {
            if (!int.TryParse(LevelInput.text, out int level) || level < 1) { Status.text = "Type a level number first"; return; }
            if (_group >= GameData.GroupCount) return;
            level = Mathf.Min(level, GameData.MaxLevel(_group));
            Panel.SetActive(false);
            App.DebugStart(_group, level);
        }

        void UnlockShape()
        {
            if (_group >= GameData.GroupCount) return;
            // Opening a group means its unlock rule is met: the needed level of the group before it is beaten.
            var rule = GameData.Unlock(_group);
            int pre = GameData.IndexOf(rule.group);
            if (!rule.IsNone && pre >= 0) CubeSave.UnlockUpTo(pre, rule.cleared);
            CubeSave.GroupOpen(_group);
            Status.text = GameData.Group(_group).name + " unlocked";
            App.DebugRefresh();
        }

        void UnlockAll()
        {
            for (int g = 0; g < GameData.GroupCount; g++) if (!GameData.Group(g).endless) CubeSave.UnlockUpTo(g, GameData.LevelCount(g));
            for (int g = 0; g < GameData.GroupCount; g++) CubeSave.GroupOpen(g);
            Status.text = "All shapes unlocked";
            App.DebugRefresh();
        }

        void ResetAll()
        {
            CubeSave.ResetAll();
            Status.text = "Progress reset";
            App.DebugHome();
        }
    }
}
