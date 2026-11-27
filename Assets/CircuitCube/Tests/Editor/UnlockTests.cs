using CircuitCube.Core;
using NUnit.Framework;
using UnityEngine;

namespace CircuitCube.Tests
{
    /// <summary>Shape unlock rules: total stars (1.4.2) with the level rule as a second way in.</summary>
    public class UnlockTests
    {
        [Test]
        public void EmptyRuleIsOpenFromTheStart()
        {
            Assert.IsTrue(new UnlockRule().Opens(0, -1));
            Assert.IsTrue(new UnlockRule { group = "" }.IsNone);
        }

        [Test]
        public void LevelRuleAloneWorksAsBefore()
        {
            var r = new UnlockRule { group = "cube", cleared = 30 };
            Assert.IsFalse(r.Opens(1000, 29));
            Assert.IsTrue(r.Opens(0, 30));
            Assert.IsTrue(r.Opens(0, -1), "a prerequisite that is not in the game opens the group, as before");
        }

        [Test]
        public void StarsOpenBeforeTheLevelRule()
        {
            var r = new UnlockRule { group = "cube", cleared = 30, stars = 45 };
            Assert.IsFalse(r.Opens(89, 10), "44½ stars is not enough: half stars are never rounded up");
            Assert.IsTrue(r.Opens(90, 10), "45 stars");
            Assert.IsTrue(r.Opens(0, 30), "the level rule still opens it");
        }

        [Test]
        public void StarsOnlyRuleIgnoresLevels()
        {
            var r = new UnlockRule { group = "cube", cleared = 0, stars = 45 };
            Assert.IsFalse(r.IsNone);
            Assert.IsFalse(r.Opens(0, 300), "cleared 0 must not open a stars rule");
            Assert.IsFalse(r.Opens(0, -1));
            Assert.IsTrue(r.Opens(90, 0));
            var noGroup = new UnlockRule { stars = 10 };
            Assert.IsFalse(noGroup.IsNone);
            Assert.IsTrue(noGroup.Opens(20, -1));
            Assert.IsFalse(noGroup.Opens(19, -1));
        }

        [Test]
        public void StarsInThePreviousShapeAreNeeded()
        {
            var r = new UnlockRule { group = "octa", cleared = 70, stars = 220, groupStars = 30 };
            Assert.IsFalse(r.Opens(1000, 5, 59), "enough stars in total, too few in the previous shape");
            Assert.IsTrue(r.Opens(440, 25, 60), "220 in total, 30 of them in the previous shape");
            Assert.IsTrue(r.Opens(0, 70, 0), "the level rule still opens it");
            Assert.IsTrue(new UnlockRule { group = "octa", stars = 220, groupStars = 30 }.Opens(440, -1, 0), "previous shape gone: total decides");
        }

        [Test]
        public void OldManifestsReadWithoutStars()
        {
            var r = JsonUtility.FromJson<UnlockRule>("{\"group\":\"cube\",\"cleared\":100}");
            Assert.AreEqual(0, r.stars);
            Assert.IsTrue(r.Opens(0, 100));
            var s = JsonUtility.FromJson<UnlockRule>("{\"group\":\"cube\",\"cleared\":30,\"stars\":45}");
            Assert.AreEqual(45, s.stars);
        }
    }
}
