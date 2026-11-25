using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    /// <summary>The Play Games web client id, needed for the server sign-in of the leaderboards.</summary>
    public class PlayGamesSettingsTests
    {
        static string Value(string file, string pattern)
        {
            var m = Regex.Match(File.ReadAllText(file), pattern, RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        [Test]
        public void WebClientIdIsSetInBothPlayGamesFiles()
        {
            // the plugin refills the asset from the text file, so an empty one there can blank the build
            string asset = Value("Assets/GooglePlayGames/Resources/PlayGamesSettings.asset", @"^  mWebClientId:[ \t]*(.*)$");
            string text = Value("ProjectSettings/GooglePlayGameSettings.txt", @"^and\.ClientId=(.*)$");
            StringAssert.EndsWith(".apps.googleusercontent.com", asset);
            Assert.AreEqual(asset, text);
        }
    }
}
