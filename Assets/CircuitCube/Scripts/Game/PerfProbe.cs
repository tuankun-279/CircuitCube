using Unity.Profiling;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Development-only frame statistics: draw calls, batches, SetPass calls, GC bytes and main-thread time, averaged
    /// (and maxed) over a number of frames. Used by the perf play-test and the debug menu; costs nothing when stopped.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        public struct Result
        {
            public int Frames;
            public float DrawCalls, Batches, SetPass, MainMs, GcBytes;
            public long MaxGcBytes, MaxDrawCalls;

            public override string ToString() =>
                $"frames {Frames}: draw calls {DrawCalls:0.#} (max {MaxDrawCalls}), batches {Batches:0.#}, setpass {SetPass:0.#}, " +
                $"main {MainMs:0.00} ms, GC {GcBytes:0} B/frame (max {MaxGcBytes})";
        }

        static PerfProbe _instance;
        ProfilerRecorder _draw, _batches, _setPass, _main, _gc;
        int _left, _total, _frames;
        double _sumDraw, _sumBatches, _sumSetPass, _sumMain, _sumGc;
        long _maxGc, _maxDraw;

        public static bool Running => _instance != null && _instance._left > 0;
        public static Result Last { get; private set; }

        /// <summary>Start measuring the next <paramref name="frames"/> frames (skips one warm-up frame).</summary>
        public static void Measure(int frames)
        {
            if (_instance == null)
            {
                var go = new GameObject("PerfProbe");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PerfProbe>();
            }
            _instance.Begin(frames);
        }

        void Begin(int frames)
        {
            Stop();
            _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _left = _total = frames + 1;
            _frames = 0;
            _sumDraw = _sumBatches = _sumSetPass = _sumMain = _sumGc = 0;
            _maxGc = _maxDraw = 0;
        }

        void LateUpdate()
        {
            if (_left <= 0) return;
            bool warmUp = _left == _total;      // the first frame after Begin has no complete sample yet
            _left--;
            if (!warmUp)
            {
                long d = _draw.LastValue, g = _gc.LastValue;
                _sumDraw += d;
                _sumBatches += _batches.LastValue;
                _sumSetPass += _setPass.LastValue;
                _sumMain += _main.LastValue * 1e-6;
                _sumGc += g;
                if (g > _maxGc) _maxGc = g;
                if (d > _maxDraw) _maxDraw = d;
                _frames++;
            }
            if (_left == 0)
            {
                float n = Mathf.Max(1, _frames);
                Last = new Result
                {
                    Frames = _frames,
                    DrawCalls = (float)(_sumDraw / n), Batches = (float)(_sumBatches / n), SetPass = (float)(_sumSetPass / n),
                    MainMs = (float)(_sumMain / n), GcBytes = (float)(_sumGc / n), MaxGcBytes = _maxGc, MaxDrawCalls = _maxDraw,
                };
                Stop();
            }
        }

        void Stop()
        {
            _draw.Dispose(); _batches.Dispose(); _setPass.Dispose(); _main.Dispose(); _gc.Dispose();
        }

        void OnDestroy() => Stop();
    }
}
