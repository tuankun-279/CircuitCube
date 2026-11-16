using System;
using System.Collections;
using UnityEngine;
#if CC_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Common;
using Google.Play.Review;
#endif

namespace CircuitCube.Game
{
    /// <summary>
    /// Google Play In-App Review: after a good run (<see cref="StreakToAsk"/> wins in a row without leaving a level
    /// unfinished, and at least <see cref="MinTotalWins"/> wins in all) the Play rating card comes up over the game,
    /// at most once every <see cref="DaysBetween"/> days. Play itself decides whether to show it (it has its own
    /// quota), and the game never learns whether the player rated. Built only with the com.google.play.review package
    /// (define CC_REVIEW); in the editor and in sideloaded builds it only logs.
    /// </summary>
    public static class AppReview
    {
        public const int StreakToAsk = 3, MinTotalWins = 8, DaysBetween = 30;
        const string StreakKey = "cc.reviewStreak", LastKey = "cc.reviewDay";

        static int Today => (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalDays;

        /// <summary>Whether to ask now. Pure, for the tests.</summary>
        public static bool ShouldAsk(int streak, int totalWins, int lastDay, int today) =>
            streak >= StreakToAsk && totalWins >= MinTotalWins && (lastDay < 0 || today - lastDay >= DaysBetween);

        /// <summary>A level was won; may ask for a rating (call when the result card is up).</summary>
        public static void Won(MonoBehaviour host, int totalWins)
        {
            int streak = PlayerPrefs.GetInt(StreakKey, 0) + 1;
            PlayerPrefs.SetInt(StreakKey, streak);
            if (!ShouldAsk(streak, totalWins, PlayerPrefs.GetInt(LastKey, -1), Today)) return;
            PlayerPrefs.SetInt(LastKey, Today);
            PlayerPrefs.SetInt(StreakKey, 0);
            GameAnalytics.Log("review_ask", ("wins", totalWins));
            host.StartCoroutine(Ask());
        }

        /// <summary>A level was left before it was solved: the run starts over.</summary>
        public static void Quit() => PlayerPrefs.SetInt(StreakKey, 0);

        static IEnumerator Ask()
        {
#if CC_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
            ReviewManager manager = null;
            PlayAsyncOperation<PlayReviewInfo, ReviewErrorCode> request = null;
            try { manager = new ReviewManager(); request = manager.RequestReviewFlow(); }
            catch (Exception e) { Debug.LogWarning("[Review] " + e.Message); yield break; }
            yield return request;
            if (request.Error != ReviewErrorCode.NoError) { Debug.LogWarning("[Review] " + request.Error); yield break; }
            var launch = manager.LaunchReviewFlow(request.GetResult());
            yield return launch;
            if (launch.Error != ReviewErrorCode.NoError) Debug.LogWarning("[Review] " + launch.Error);
#else
            Debug.Log("[Review] the Play rating card would show now");
            yield break;
#endif
        }
    }
}
