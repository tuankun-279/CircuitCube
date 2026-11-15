using CircuitCube.Core;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// Debug menu (prefab Prefabs/UI/DebugMenu.prefab). F1 shows or hides it. Only in the editor and development
    /// builds; release builds switch it off. Pick a shape group, type a level, Play; or unlock shapes.
    /// Keys: F2 dark / light, F3 skip ads, F5 reset every lesson, F6 (editor) play the Level Lab import zips.
    /// Levels started here show their lessons again.
    /// </summary>
    public sealed class DebugMenu : MonoBehaviour
    {
        public CircuitCubeApp App;
        public GameObject Panel;
        public Button[] GroupButtons;      // one per shape group, in group order
        public Image[] GroupMarks;         // highlight behind the selected group button
        public TMP_InputField LevelInput;
        public Button PlayButton, UnlockShapeButton, UnlockAllButton, ResetButton, CloseButton;
        public TMP_Text Status;

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
            // F3 skips the rewarded ads in front of hints and undos
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                RewardedAds.SkipAds = !RewardedAds.SkipAds;
                Debug.Log("[CircuitCube] ads " + (RewardedAds.SkipAds ? "skipped" : "on"));
                if (Status != null) Status.text = RewardedAds.SkipAds ? "Ads skipped (F3)" : "Ads on (F3)";
            }
            // F5 forgets every lesson (tutorial on Moonbase 1-3, colours, mixes, the HUD mix formulas)
            if (kb != null && kb.f5Key.wasPressedThisFrame)
            {
                CubeSave.TutorialStep = 0;
                CubeSave.Tips = 0;
                CubeSave.MixWins = 0;
                CubeSave.SeenMix = false;
                CubeSave.SeenHowTo = false;
                Debug.Log("[CircuitCube] lessons reset");
                if (Status != null) Status.text = "Lessons reset (F5)";
            }
#if UNITY_EDITOR
            // F6 plays the Level Lab import zips (Tools/LevelLab/import) instead of the published levels, or stops
            if (kb != null && kb.f6Key.wasPressedThisFrame)
            {
                bool on = !GameData.Previewing;
                if (on) EditorPreview(); else System.IO.Directory.Delete(GameData.PreviewRoot, true);
                GameData.Reload();
                if (App != null) App.DebugRefresh();
                Debug.Log("[CircuitCube] import preview " + (on ? "on" : "off"));
                if (Status != null) Status.text = on ? "Playing the import zips (F6)" : "Playing the published levels (F6)";
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>Same as Tools/Circuit Cube/Data/Preview Level Lab Imports (the editor assembly is out of reach here).</summary>
        static void EditorPreview()
        {
            var m = System.Type.GetType("CircuitCube.EditorTools.DataTools, CircuitCube.Editor")?.GetMethod("PreviewImports");
            if (m != null) m.Invoke(null, null);
        }
#endif

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
