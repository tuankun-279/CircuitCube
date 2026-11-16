using System;
using System.Text;
using UnityEngine;
#if CC_PLAYGAMES && UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

namespace CircuitCube.Game
{
    /// <summary>
    /// Progress in the cloud with Play Games Services saved games, so a new or reset phone gets it back. At start the
    /// game signs in quietly (Play Games v2 signs in on its own; there is no sign-in button), reads the cloud record,
    /// merges it into the device (<see cref="CubeSave.MergeCloud"/>: furthest level and best stars win, nothing is
    /// lost) and writes the merged record back. After that a win marks the record dirty and it is written at most once
    /// a minute, and whenever the game goes to the background. Built only with the com.google.play.games package
    /// (define CC_PLAYGAMES) and the Play Games app id set up (Window > Google Play Games > Setup); otherwise it does
    /// nothing and the game plays as before.
    /// </summary>
    public static class CloudSave
    {
        const string SlotName = "progress";
        const float MinSecondsBetweenWrites = 60f;

        public static bool SignedIn { get; private set; }
        static bool _started, _dirty, _busy, _pulled;
        static float _lastWrite = -1000f;

        public static void Init()
        {
            if (_started) return;
            _started = true;
#if CC_PLAYGAMES && UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!GameInfo.ApplicationIdInitialized()) { Debug.LogWarning("[CloudSave] off: no Play Games app id (Window > Google Play Games > Setup)"); return; }
                PlayGamesPlatform.Activate();
                PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    SignedIn = status == SignInStatus.Success;
                    GameAnalytics.Log("cloud_signin", ("ok", SignedIn));
                    if (SignedIn) Pull();
                });
            }
            catch (Exception e) { Debug.LogWarning("[CloudSave] off: " + e.Message); }
#endif
        }

        /// <summary>Progress changed (a win); written soon.</summary>
        public static void MarkDirty() => _dirty = true;

        /// <summary>Called every frame: writes a dirty record once the minute is up.</summary>
        public static void Tick()
        {
            if (_dirty && Time.realtimeSinceStartup - _lastWrite >= MinSecondsBetweenWrites) Push();
        }

        /// <summary>The game is going to the background: write now if anything changed.</summary>
        public static void Flush() { if (_dirty) Push(); }

        static byte[] Encode(CloudProgress p) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));

        static CloudProgress Decode(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            try { return JsonUtility.FromJson<CloudProgress>(Encoding.UTF8.GetString(data)); }
            catch (Exception e) { Debug.LogWarning("[CloudSave] unreadable record: " + e.Message); return null; }
        }

#if CC_PLAYGAMES && UNITY_ANDROID && !UNITY_EDITOR
        static ISavedGameClient Client => PlayGamesPlatform.Instance.SavedGame;

        static void Open(Action<ISavedGameMetadata> then)
        {
            _busy = true;
            Client.OpenWithAutomaticConflictResolution(SlotName, DataSource.ReadNetworkOnly, ConflictResolutionStrategy.UseLongestPlaytime,
                (status, meta) =>
                {
                    if (status != SavedGameRequestStatus.Success) { _busy = false; Debug.LogWarning("[CloudSave] open: " + status); return; }
                    then(meta);
                });
        }

        static void Pull()
        {
            if (_busy) return;
            Open(meta => Client.ReadBinaryData(meta, (status, data) =>
            {
                if (status != SavedGameRequestStatus.Success) { _busy = false; Debug.LogWarning("[CloudSave] read: " + status); return; }
                _pulled = true;
                bool changed = CubeSave.MergeCloud(Decode(data));
                GameAnalytics.Log("cloud_pull", ("bytes", data != null ? data.Length : 0), ("changed", changed));
                Commit(meta);   // the merged record goes back up
            }));
        }

        static void Push()
        {
            if (_busy || !SignedIn || !_pulled) return;
            _dirty = false;
            _lastWrite = Time.realtimeSinceStartup;
            Open(Commit);
        }

        static void Commit(ISavedGameMetadata meta)
        {
            var record = CubeSave.ExportCloud();
            var update = new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription("Circuit Cube progress")
                .WithUpdatedPlayedTime(TimeSpan.FromSeconds(Math.Max(1, record.Weight())))   // the furthest record wins a conflict
                .Build();
            Client.CommitUpdate(meta, update, Encode(record), (status, _) =>
            {
                _busy = false;
                if (status != SavedGameRequestStatus.Success) { _dirty = true; Debug.LogWarning("[CloudSave] write: " + status); }
            });
        }
#else
        static void Push() { _dirty = false; }
#endif
    }
}
