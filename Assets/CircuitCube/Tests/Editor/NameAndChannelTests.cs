using CircuitCube.Core;
using CircuitCube.Game;
using NUnit.Framework;
using Studio.Tracking;
using UnityEngine;

namespace CircuitCube.Tests
{
    /// <summary>1.5.0: build channels and services environments, the name rules file, the Vietnamese table.</summary>
    public class NameAndChannelTests
    {
        [Test]
        public void OnlyPlayInstallsAreRealPlayers()
        {
            Assert.AreEqual(ChannelRules.Editor, ChannelRules.Pick(true, true, false, ChannelRules.PlayInstaller));
            Assert.AreEqual(ChannelRules.TestLab, ChannelRules.Pick(false, false, true, ChannelRules.PlayInstaller));
            Assert.AreEqual(ChannelRules.Dev, ChannelRules.Pick(false, true, false, ChannelRules.PlayInstaller));
            Assert.AreEqual(ChannelRules.Sideload, ChannelRules.Pick(false, false, false, ""));
            Assert.AreEqual(ChannelRules.Sideload, ChannelRules.Pick(false, false, false, "com.amazon.venezia"));
            Assert.AreEqual(ChannelRules.Play, ChannelRules.Pick(false, false, false, ChannelRules.PlayInstaller));
            foreach (var c in ChannelRules.All)
                Assert.AreEqual(c == ChannelRules.Play ? "production" : "development", ChannelRules.Environment(c), c);
        }

        [Test]
        public void DailyBoardCountsPlayAndOldClients()
        {
            Assert.AreEqual("daily_players", ChannelRules.DailyBoard(ChannelRules.Play, "daily_players", "daily_players_dev"));
            Assert.AreEqual("daily_players", ChannelRules.DailyBoard(null, "daily_players", "daily_players_dev"), "builds before 1.5.0 send no channel");
            Assert.AreEqual("daily_players_dev", ChannelRules.DailyBoard(ChannelRules.Editor, "daily_players", "daily_players_dev"));
            Assert.AreEqual("daily_players_dev", ChannelRules.DailyBoard(ChannelRules.Normalize("hacked"), "daily_players", "daily_players_dev"));
        }

        [Test]
        public void TheEditorUsesDevelopment()
        {
            Assert.AreEqual(ChannelRules.Editor, BuildChannel.Current);
            Assert.AreEqual("development", Ranks.Environment);
            Assert.IsFalse(BuildChannel.CountsAsPlayer);
        }

        [Test]
        public void NameRulesComeFromTheDataFile()
        {
            var file = Resources.Load<TextAsset>("NameRules");
            Assert.IsNotNull(file, "Resources/NameRules.txt");
            var rules = NameRules.Parse(file.text);
            Assert.Greater(rules.Count, 30);
            Assert.AreEqual(NameVerdict.Ok, rules.Check("Tuan Pro!!", out var clean));
            Assert.AreEqual("Tuan_Pro", clean);
            Assert.AreEqual(NameVerdict.Blocked, rules.Check("xFuCkx", out _));
            Assert.AreEqual(NameVerdict.Blocked, rules.Check("Đm_ban", out _), "Vietnamese marks are folded");
            Assert.AreEqual(NameVerdict.Blocked, rules.Check("Địt mẹ", out _));
            Assert.AreEqual(NameVerdict.Ok, rules.Check("Colonel", out _), "short words only as whole words");
            Assert.AreEqual(NameVerdict.Ok, rules.Check("Grape_Lover", out _));
            Assert.AreEqual(NameVerdict.Reserved, rules.Check("Admin", out _));
            Assert.AreEqual(NameVerdict.Reserved, rules.Check("Circuit Cube", out _), "team-like names, with _ removed");
            Assert.AreEqual(NameVerdict.Reserved, rules.Check("the_moderator_1", out _));
            Assert.AreEqual(NameVerdict.Empty, rules.Check("  !!  ", out _));
            Assert.AreEqual(NameRules.MaxLength, NameRules.Tidy(new string('a', 40)).Length);
        }

        [Test]
        public void VietnameseTableLoads()
        {
            // a key written twice would throw here, when the table is first used
            Assert.Greater(LocVi.Table.Count, 200);
            Assert.AreEqual("Đổi tên", LocVi.Table["Rename"]);
            Assert.AreEqual("Khối", LocVi.Table["Shapes"]);
            Assert.AreEqual("Tiến độ", LocVi.Table["Progress"]);
        }
    }
}
