namespace Studio.Tracking
{
    /// <summary>
    /// Where a build came from, as the game reports it to its servers and to analytics. Plain C# with no engine
    /// types, so a server module (for example a Cloud Code C# module) can compile this file and apply the same rules.
    /// </summary>
    public static class ChannelRules
    {
        /// <summary>Running inside the Unity Editor.</summary>
        public const string Editor = "editor";
        /// <summary>A development build (Development Build ticked) on a device.</summary>
        public const string Dev = "dev";
        /// <summary>Google's test devices (Firebase Test Lab, the Play pre-launch report robots).</summary>
        public const string TestLab = "testlab";
        /// <summary>A release build that was not installed by Google Play (an APK copied from the PC, another store).</summary>
        public const string Sideload = "sideload";
        /// <summary>Installed by Google Play: any track, internal and closed testing included.</summary>
        public const string Play = "play";

        /// <summary>The Android installer package of Google Play.</summary>
        public const string PlayInstaller = "com.android.vending";

        /// <summary>Unity Gaming Services environment names.</summary>
        public const string ProductionEnvironment = "production";
        public const string DevelopmentEnvironment = "development";

        public static readonly string[] All = { Editor, Dev, TestLab, Sideload, Play };

        /// <summary>
        /// The channel from what the build knows about itself. Test-lab devices win over the installer (the robots
        /// install from Play too), a development build wins over the installer (a debug build pushed to the track
        /// is still a test build).
        /// </summary>
        public static string Pick(bool editor, bool developmentBuild, bool testLab, string installer)
        {
            if (editor) return Editor;
            if (testLab) return TestLab;
            if (developmentBuild) return Dev;
            return installer == PlayInstaller ? Play : Sideload;
        }

        /// <summary>Only Google Play installs are real players: they alone are counted and use production.</summary>
        public static bool CountsAsPlayer(string channel) => channel == Play;

        /// <summary>The services environment a build of this channel signs in to.</summary>
        public static string Environment(string channel) => CountsAsPlayer(channel) ? ProductionEnvironment : DevelopmentEnvironment;

        /// <summary>A channel value received from a client, made safe to store: unknown values read as sideload.</summary>
        public static string Normalize(string channel)
        {
            if (channel == null) return Sideload;
            foreach (var c in All) if (c == channel) return c;
            return Sideload;
        }

        /// <summary>
        /// The daily board a check-in goes to: the counted board for real players, the test board for every other
        /// channel. A client too old to send a channel (null) was a store install, so it is counted.
        /// </summary>
        public static string DailyBoard(string channel, string countedBoard, string testBoard) =>
            channel == null || CountsAsPlayer(channel) ? countedBoard : testBoard;
    }
}
