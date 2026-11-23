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
    /// The Win row ends the level in play (or starts one) with a set result: 1.5, 2.5 or 3 stars, a three-star streak,
    /// Expert or an Expert streak, to check each result screen.
    /// Keys: F2 dark / light, F3 skip ads, F4 sample leaderboards, F5 reset every lesson.
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
        [Tooltip("Win with a set result, in CircuitCubeApp.DebugResults order (1.5 stars ... Expert streak).")]
        public Button[] WinButtons = new Button[0];

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
            for (int i = 0; i < WinButtons.Length; i++)
            {
                int k = i;
                if (WinButtons[i] != null) WinButtons[i].onClick.AddListener(() => WinAs(k));
            }
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
            // F4 fills the leaderboard screens with sample boards and a sample week
            if (kb != null && kb.f4Key.wasPressedThisFrame && App != null)
            {
                App.DebugRanks(!RanksDebug.On);
                if (Status != null) Status.text = RanksDebug.On ? "Sample leaderboards on (F4)" : "Sample leaderboards off (F4)";
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

        /// <summary>Win the level in play with a set result; from the menus, start the level typed here (or the next one) first.</summary>
        void WinAs(int kind)
        {
            if (_group >= GameData.GroupCount) return;
            int level = int.TryParse(LevelInput.text, out int l) && l >= 1 ? l : CubeSave.Unlocked(_group);
            level = Mathf.Clamp(level, 1, GameData.MaxLevel(_group));
            Panel.SetActive(false);
            App.DebugWinAs(kind, _group, level);
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
