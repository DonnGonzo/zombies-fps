using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Editor
{
    public static class ImportedModelValidation
    {
        static TestRunnerApi _tests;
        public static void BuildPlayer()
        {
            int version = 1;
            while (Directory.Exists($"Builds/Windows-Test-{version:00}")) version++;
            string output = $"Builds/Windows-Test-{version:00}/ZombiesFPS.exe";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Prototype.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            File.WriteAllText("Temp/model-import/player-build.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\n" + output);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows test build failed: " + report.summary.result);
        }
        public static void RunTests()
        {
            _tests = ScriptableObject.CreateInstance<TestRunnerApi>();
            _tests.RegisterCallbacks(new Results());
            _tests.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ZombiesFPS.Tests.EditMode" } }));
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, "Temp/model-import/tests.xml");
                File.WriteAllText("Temp/model-import/tests.txt", $"Passed {result.PassCount}; failed {result.FailCount}; skipped {result.SkipCount}\n{result.Message}");
                UnityEngine.Object.DestroyImmediate(_tests);
            }
        }

        public static void Review()
        {
            Directory.CreateDirectory("Temp/model-import/review");
            foreach (var room in new[]
            {
                (zone: 3, build: "BuildChapel", dress: "", eye: new Vector3(0f,2f,-18f), target: new Vector3(0f,2f,2f)),
                (zone: 13, build: "BuildPlagueWard", dress: "PlagueWardMakeover", eye: new Vector3(15f,2f,-4f), target: new Vector3(0f,1.3f,5f)),
                (zone: 12, build: "BuildGaol", dress: "GaolMakeover", eye: new Vector3(-0.5f,2f,-0.5f), target: new Vector3(-5f,1.2f,-6f)),
                (zone: 2, build: "BuildGarage", dress: "GarageMakeover", eye: new Vector3(0f,2f,-12f), target: new Vector3(-8f,1.7f,-6f)),
                (zone: 6, build: "BuildLoadingDock", dress: "LoadingDockMakeover", eye: new Vector3(1f,2.3f,7f), target: new Vector3(-6.5f,1.6f,-1f)),
                (zone: 1, build: "BuildDepot", dress: "DepotMakeover", eye: new Vector3(-6f,3.5f,3f), target: new Vector3(-7f,0.5f,-11f)),
            })
            {
                var root = new GameObject("Review " + room.zone);
                var preview = new PreviewRenderUtility();
                bool photos = SurfaceTextures.PhotosEnabled;
                try
                {
                    SurfaceTextures.PhotosEnabled = true;
                    var map = new ZoneMap();
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(ZoneMap).GetProperty("Root").SetValue(map, root.transform);
                    var field = typeof(ZoneMap).GetField("_decor", flags);
                    var decor = Activator.CreateInstance(field.FieldType, true);
                    field.SetValue(map, decor);
                    foreach (var rect in ZoneMap.ZoneRects(room.zone))
                        Greybox.Box("Floor", root.transform, new Vector3(rect.center.x, -0.15f, rect.center.y), new Vector3(rect.width,0.3f,rect.height), new Color(0.3f,0.32f,0.32f), surface: Surface.Concrete);
                    foreach (var wall in ZoneMap.Walls)
                    {
                        var mid = (wall.A + wall.B) / 2f;
                        if ((new Vector3(mid.x, 0f, mid.y) - ZoneMap.ZoneCenter(room.zone)).sqrMagnitude > 45f * 45f) continue;
                        float h = wall.Top - wall.Bottom;
                        Greybox.Box("Wall", root.transform, new Vector3(mid.x, wall.Bottom + h / 2f, mid.y), wall.AlongX
                            ? new Vector3(wall.Length,h,ZoneMap.WallThickness) : new Vector3(ZoneMap.WallThickness,h,wall.Length),
                            new Color(0.48f,0.46f,0.42f), surface: Surface.Masonry);
                    }
                    typeof(ZoneMap).GetMethod(room.build, flags).Invoke(map, null);
                    if (room.dress.Length > 0) typeof(ZoneMap).GetMethod(room.dress, flags).Invoke(map, null);
                    if (room.zone == 3) typeof(ZoneMap).GetMethod("ChapelWindows", flags).Invoke(map, null);
                    if (room.zone == 1) typeof(ZoneMap).GetMethod("BuildCasinoAndResearchProps", flags).Invoke(map, null);
                    field.FieldType.GetMethod("Build").Invoke(decor, new object[] { root.transform });
                    preview.AddSingleGO(root);
                    preview.camera.transform.position = ZoneMap.ZoneCenter(room.zone) + room.eye;
                    preview.camera.transform.LookAt(ZoneMap.ZoneCenter(room.zone) + room.target);
                    preview.camera.nearClipPlane = 0.05f; preview.camera.farClipPlane = 160f;
                    preview.camera.fieldOfView = 65f;
                    preview.camera.clearFlags = CameraClearFlags.Color;
                    preview.camera.backgroundColor = new Color(0.08f,0.1f,0.15f);
                    preview.lights[0].intensity = 1.1f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(50f,-30f,0f);
                    preview.lights[1].intensity = 0.4f;
                    preview.ambientColor = new Color(0.45f,0.45f,0.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1440,900));
                    preview.Render();
                    var texture = preview.EndStaticPreview();
                    File.WriteAllBytes("Temp/model-import/review/" + room.zone + ".png", texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                finally
                {
                    preview.Cleanup();
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
                    SurfaceTextures.PhotosEnabled = photos;
                }
            }
        }
    }
}
