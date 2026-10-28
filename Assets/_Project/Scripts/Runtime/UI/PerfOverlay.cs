using System.Text;
using Unity.Profiling;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The Show FPS read-out (Settings > Graphics): the frame rate in the top right corner, or with Details also
    /// the slowest recent frame (stutters), CPU time, draw calls, triangles, garbage made per frame, zombies and
    /// lights. The text is rebuilt a few times a second, so the read-out itself costs next to nothing.
    /// Made automatically; nothing needs to add it.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        const float RefreshEvery = 0.5f;
        const float WorstWindow = 2f;

        ProfilerRecorder _mainThread, _renderThread, _batches, _setPass, _drawCalls, _triangles, _gcAlloc;
        readonly StringBuilder _text = new StringBuilder(256);
        readonly FrameTiming[] _timing = new FrameTiming[1];
        string _shown = "";
        float _sinceRefresh, _frameSum, _worst, _worstShown, _worstAge, _fps;
        int _frames, _lights;
        float _lightsAt = -10f;
        int _mode = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindAnyObjectByType<PerfOverlay>() != null) return;
            var go = new GameObject("PerfOverlay") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<PerfOverlay>();
        }

        void OnDisable() => StopRecorders();

        void StartRecorders()
        {
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 15);
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
        }

        void StopRecorders()
        {
            _mainThread.Dispose();
            _renderThread.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _drawCalls.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
        }

        void Update()
        {
            int mode = GameSettings.PerfOverlay;
            if (mode != _mode)
            {
                // The counters only run while the details are showing.
                if (_mode == 2) StopRecorders();
                if (mode == 2) StartRecorders();
                _mode = mode;
                _sinceRefresh = RefreshEvery;
            }
            if (mode == 0) return;

            float dt = Time.unscaledDeltaTime;
            _frameSum += dt;
            _frames++;
            _worst = Mathf.Max(_worst, dt);
            _worstAge += dt;
            _sinceRefresh += dt;
            if (mode == 2) FrameTimingManager.CaptureFrameTimings();
            if (_sinceRefresh < RefreshEvery) return;

            _fps = _frames > 0 && _frameSum > 0f ? _frames / _frameSum : 0f;
            float avgMs = _frames > 0 ? _frameSum / _frames * 1000f : 0f;
            // The slowest frame of the last couple of seconds, so a stutter stays on screen long enough to read.
            if (_worstAge >= WorstWindow || _worst > _worstShown)
            {
                _worstShown = _worst;
                _worstAge = 0f;
                _worst = 0f;
            }
            _frameSum = 0f;
            _frames = 0;
            _sinceRefresh = 0f;

            _text.Clear();
            _text.Append(Mathf.RoundToInt(_fps)).Append(" FPS");
            if (mode == 2)
            {
                _text.Append("   ").Append(avgMs.ToString("0.0")).Append(" ms\n");
                _text.Append("Slowest frame ").Append((_worstShown * 1000f).ToString("0.0")).Append(" ms\n");
                _text.Append("CPU ").Append(Ms(_mainThread)).Append("   Render ").Append(Ms(_renderThread));
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].gpuFrameTime > 0.0)
                    _text.Append("   GPU ").Append(_timing[0].gpuFrameTime.ToString("0.0")).Append(" ms");
                _text.Append('\n');
                _text.Append("Batches ").Append(Count(_batches)).Append("   SetPass ").Append(Count(_setPass))
                    .Append("   Draws ").Append(Count(_drawCalls)).Append('\n');
                _text.Append("Triangles ").Append(Count(_triangles)).Append("   Garbage ").Append(Kb(_gcAlloc)).Append('\n');
                if (Time.unscaledTime - _lightsAt > 2f)
                {
                    _lightsAt = Time.unscaledTime;
                    _lights = 0;
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.isActiveAndEnabled) _lights++;
                }
                _text.Append("Zombies ").Append(NetZombie.Spawned.Count).Append("   Lights ").Append(_lights);
            }
            _shown = _text.ToString();
        }

        static string Ms(ProfilerRecorder r)
        {
            if (!r.Valid || r.Count == 0) return "-";
            double sum = 0;
            int n = r.Count;
            for (int i = 0; i < n; i++) sum += r.GetSample(i).Value;
            return (sum / n * 1e-6).ToString("0.0") + " ms";
        }

        static string Count(ProfilerRecorder r)
        {
            if (!r.Valid) return "-";
            long v = r.LastValue;
            return v >= 1000000 ? (v / 1e6).ToString("0.0") + "M" : v >= 10000 ? (v / 1000.0).ToString("0") + "k" : v.ToString();
        }

        static string Kb(ProfilerRecorder r)
        {
            if (!r.Valid || r.Count == 0) return "-";
            long max = 0;
            for (int i = 0; i < r.Count; i++) max = System.Math.Max(max, r.GetSample(i).Value);
            return (max / 1024.0).ToString("0.0") + " KB";
        }

        void OnGUI()
        {
            if (_mode <= 0 || string.IsNullOrEmpty(_shown) || Event.current.type != EventType.Repaint) return;
            float u = Ui.MenuU;
            Ui.EnsureStyles(u);
            GUI.depth = -1000;
            var style = _mode == 2 ? Ui.Tiny : Ui.Small;
            var size = style.CalcSize(new GUIContent(_shown));
            float pad = 6 * u;
            // In the top right corner, left of the missions panel once the details make it tall.
            float right = _mode == 2 ? Screen.width - 560 * u : Screen.width - 8 * u;
            var r = new Rect(right - size.x - pad * 2f, 2 * u, size.x + pad * 2f, size.y + pad);
            Ui.Fill(r, new Color(0f, 0f, 0f, 0.55f));
            var color = _fps >= 100f ? Ui.Good : _fps >= 55f ? new Color(1f, 0.85f, 0.3f) : Ui.Bad;
            Ui.Label(new Rect(r.x + pad, r.y + pad / 2f, size.x, size.y), _shown, style, _mode == 2 ? Ui.Text : color, TextAnchor.UpperLeft);
        }
    }
}
