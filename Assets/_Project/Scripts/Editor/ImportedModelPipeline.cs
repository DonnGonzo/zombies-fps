using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombiesFPS.Editor
{
    // File requests let the asset preparation script use the already-open editor without opening a second project.
    [InitializeOnLoad]
    public static class ImportedModelPipeline
    {
        const string Source = "Assets/_Project/ImportedModels";
        const string Request = "Temp/model-import/request.txt";
        static ImportedModelPipeline() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            string job = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (job == "inspect") Inspect();
                if (job == "build") Build();
                if (job == "october") Build(true);
                if (job == "clips") ReportClips();
                if (job == "poses") InspectPoses();
                if (job == "tests") ImportedModelValidation.RunTests();
                if (job == "review") ImportedModelValidation.Review();
                if (job == "player") ImportedModelValidation.BuildPlayer();
                if (job == "voices") VoiceExport.Export();
                if (job == "weapons") VoiceExport.WeaponPreviews();
                File.WriteAllText("Temp/model-import/result.txt", "OK " + job);
            }
            catch (Exception e) { File.WriteAllText("Temp/model-import/result.txt", e.ToString()); Debug.LogException(e); }
        }

        static void InspectPoses()
        {
            var report = new StringBuilder();
            foreach (string slug in new[] { "gun-m4a1", "desert-eagle-gun", "low-poly-9mm-hand-gun", "prison-cell" })
            {
                string path = Source + "/" + slug + "/model.fbx";
                var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    report.AppendLine(slug + " root " + go.transform.eulerAngles);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) report.AppendLine(r.name + " " + r.bounds.ToString("F4"));
                    if (slug != "low-poly-9mm-hand-gun") continue;
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                    for (int i = 0; i <= 16; i++)
                    {
                        clip.SampleAnimation(go, i * 0.5f);
                        report.AppendLine("time " + i * 0.5f);
                        foreach (var r in go.GetComponentsInChildren<Renderer>().Where(r => r.name == "Magazine" || r.name == "polySurface1")) report.AppendLine(r.name + " " + r.bounds.ToString("F4"));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            File.WriteAllText("Temp/model-import/poses.txt", report.ToString());
        }

        [Serializable] sealed class TextureSet
        {
            public string slug, key, albedo, normal, mask, ao, emission;
        }
        [Serializable] sealed class TextureManifest { public TextureSet[] materials; }
        static readonly Dictionary<string, float> Guns = new Dictionary<string, float>
        {
            ["gun-m4a1"] = 0.78f, ["desert-eagle-gun"] = 0.27f, ["gun-rhino-smg"] = 0.6f,
            ["heavy-pulse-machine-gun"] = 0.88f, ["low-poly-9mm-hand-gun"] = 0.23f,
            ["submachine-gun"] = 0.38f, ["tommy-gun"] = 0.8f, ["mp5-gun"] = 0.68f,
        };
        static Quaternion Orientation(string slug)
        {
            switch (slug)
            {
                case "gun-m4a1": case "submachine-gun": case "tommy-gun": case "prison-cell": return Quaternion.Euler(0f, 90f, 0f);
                case "church-bench-2": case "hl2-beta-air-exchange-truck-remade": case "gun-rhino-smg": return Quaternion.Euler(0f, 180f, 0f);
                default: return Quaternion.identity;
            }
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        [MenuItem("Zombies FPS/Imported Models/Build prefabs and previews")]
        public static void Build(bool octoberOnly = false)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var sets = JsonUtility.FromJson<TextureManifest>(File.ReadAllText(Source + "/materials.json")).materials;
            const string output = "Assets/_Project/Resources/ImportedModels";
            Directory.CreateDirectory(output);
            Directory.CreateDirectory("Temp/model-import/previews");
            var report = new StringBuilder();
            foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { Source }).Select(AssetDatabase.GUIDToAssetPath))
            {
                string slug = Path.GetFileName(Path.GetDirectoryName(path));
                if (octoberOnly && !OctoberModels.Contains(slug)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                var root = new GameObject(slug);
                var model = UnityEngine.Object.Instantiate(asset, root.transform);
                if (slug == "prison-cell")
                {
                    var door = model.GetComponentsInChildren<Transform>().First(t => t.name == "CellDoor");
                    var bars = model.GetComponentsInChildren<Renderer>().First(r => r.name == "BarswDoorHole");
                    var renderer = door.GetComponent<Renderer>();
                    var center = renderer.bounds.center;
                    door.RotateAround(center, Vector3.up, 71.40923f);
                    door.position += new Vector3(bars.bounds.center.x - renderer.bounds.center.x, 0f, 0.24f - renderer.bounds.center.z);
                }
                if (slug == "low-poly-9mm-hand-gun")
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip != null) clip.SampleAnimation(model, clip.length);
                }
                model.transform.localRotation = Orientation(slug) * model.transform.localRotation;
                bool nativeAnimation = OctoberModels.Contains(slug) && AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Any(c => !c.name.StartsWith("__preview__"));
                bool animated = slug == "zombie-hazmat";
                try
                {
                    foreach (var t in model.GetComponentsInChildren<Transform>().Reverse())
                    {
                        bool remove = slug == "hl2-beta-air-exchange-truck-remade" && t.name == "Plane001"
                            || slug == "low-poly-9mm-hand-gun" && t.name.StartsWith("Gating_Shell")
                            || slug == "tommy-gun" && t.name.StartsWith("bullet_low")
                            || slug == "prison-cell" && new[] { "BarswDoorHole1", "CellDoor1", "CellSide3", "Pillow1", "PrisonBed1", "Sandalye1" }.Contains(t.name);
                        if (remove) UnityEngine.Object.DestroyImmediate(t.gameObject);
                    }
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = r.sharedMaterials.Select(m => MakeMaterial(slug, m, sets)).ToArray();
                    var b = BoundsOf(root);
                    float scale = Guns.TryGetValue(slug, out float length) ? length / b.size.z : 1f / Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (animated) scale = 1f;
                    model.transform.localScale *= scale;
                    if (slug == "low-poly-9mm-hand-gun") BakeMagazineMotion(path, scale, output + "/9mmReload.asset");
                    b = BoundsOf(root);
                    if (!animated)
                    {
                        var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                        if (Guns.ContainsKey(slug)) offset = new Vector3(-b.center.x, Weapons.GunModels.SightY - b.max.y, (length > 0.5f ? -0.18f : -0.08f) - b.min.z);
                        model.transform.position += offset;
                        if (nativeAnimation) NativeAnimation(model, path, output, slug);
                        else MergeStatic(root, model, output, slug);
                    }
                    else
                    {
                        // Keep the animation binding paths intact: the prefab itself is the imported rig root.
                        model.transform.SetParent(null);
                        UnityEngine.Object.DestroyImmediate(root);
                        root = model;
                        root.name = slug;
                        MergeSkin(root, output + "/" + slug + "_Skin.asset");
                        Directory.CreateDirectory(output + "/HazmatAnimations");
                        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                        {
                            string clipPath = output + "/HazmatAnimations/" + clip.name + ".anim";
                            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                            if (copy == null) { copy = new AnimationClip(); AssetDatabase.CreateAsset(copy, clipPath); }
                            EditorUtility.CopySerialized(clip, copy);
                            var settings = AnimationUtility.GetAnimationClipSettings(copy);
                            settings.loopTime = true;
                            AnimationUtility.SetAnimationClipSettings(copy, settings);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, output + "/" + slug + ".prefab");
                    b = BoundsOf(root);
                    report.AppendLine(slug + " " + b.ToString("F4"));
                    RenderPreview(root, "Temp/model-import/previews/" + slug + ".png");
                }
                finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/model-import/built.txt", report.ToString());
        }

        static readonly HashSet<string> OctoberModels = new HashSet<string> { "a-slot-machine", "animated-oak-trees", "gambling-table", "geometrynodes-scifi-sphere", "mp5-gun", "sci-fi-capsule", "stylized-old-mage-animated-character", "wizard-spell" };
        static void NativeAnimation(GameObject model, string source, string output, string slug)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            string path = output + "/" + slug + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (controller == null) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var loop = machine.AddState("Authored animation"); loop.motion = clip;
            machine.defaultState = loop;
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            // Native generic clips keep all authored transforms, skin weights and blendshapes intact.
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips) c.loopTime = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        static void ReportClips()
        {
            var report = new StringBuilder();
            foreach (string slug in OctoberModels)
                foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { Source + "/" + slug }).Select(AssetDatabase.GUIDToAssetPath))
                    foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                        report.AppendLine($"{slug} {clip.name} {clip.length:F2}s");
            foreach (string slug in OctoberModels)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/ImportedModels/" + slug + ".prefab");
                if (prefab == null) continue;
                int verts = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh != null ? f.sharedMesh.vertexCount : 0)
                    + prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0);
                report.AppendLine($"{slug} vertices {verts}");
            }
            File.WriteAllText("Temp/model-import/clips.txt", report.ToString());
        }

        static Material MakeMaterial(string slug, Material original, TextureSet[] sets)
        {
            string key = original != null ? original.name : "default";
            var set = sets.FirstOrDefault(s => s.slug == slug && s.key == key) ?? sets.FirstOrDefault(s => s.slug == slug && s.key == "default");
            string folder = Source + "/" + slug + "/Materials";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = set != null ? Color.white : original != null ? original.color : Color.gray;
            mat.SetFloat("_Glossiness", slug == "prison-cell" ? 0.25f : 0.32f);
            mat.SetFloat("_Metallic", slug == "prison-cell" && key.Contains("Cell") ? 0.65f : 0f);
            mat.enableInstancing = true;
            if (set != null)
            {
                SetMap(mat, "_MainTex", set.albedo, null);
                SetMap(mat, "_BumpMap", set.normal, "_NORMALMAP");
                SetMap(mat, "_MetallicGlossMap", set.mask, "_METALLICGLOSSMAP");
                SetMap(mat, "_OcclusionMap", set.ao, null);
                SetMap(mat, "_EmissionMap", set.emission, "_EMISSION");
                if (!string.IsNullOrEmpty(set.emission)) mat.SetColor("_EmissionColor", Color.white * 1.5f);
            }
            if (slug == "geometrynodes-scifi-sphere")
            {
                mat.color = key == "emission" ? new Color(0.2f, 0.8f, 1f) : new Color(0.18f, 0.24f, 0.3f);
                mat.SetFloat("_Metallic", 0.5f);
                if (key == "emission") { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0.1f, 1f, 2f)); }
            }
            if (key == "foliage" || slug == "wizard-spell" || key == "FX1" || key.StartsWith("Hologram") || key == "Halo")
            {
                mat.SetFloat("_Mode", 1); mat.SetInt("_SrcBlend", 1); mat.SetInt("_DstBlend", 0); mat.SetInt("_ZWrite", 1);
                mat.EnableKeyword("_ALPHATEST_ON"); mat.SetFloat("_Cutoff", 0.25f); mat.renderQueue = 2450;
            }
            if (slug == "sci-fi-capsule" && key == "Glass")
            {
                mat.color = new Color(0.35f, 0.75f, 0.8f, 0.18f); mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", 1); mat.SetInt("_DstBlend", 10); mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BakeMagazineMotion(string source, float scale, string output)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try
            {
                var body = go.GetComponentsInChildren<Transform>().First(t => t.name == "polySurface1");
                var magazine = go.GetComponentsInChildren<Renderer>().First(t => t.name == "Magazine");
                clip.SampleAnimation(go, clip.length);
                var reference = body.localToWorldMatrix;
                var seated = magazine.bounds.center;
                var motion = AssetDatabase.LoadAssetAtPath<Weapons.ImportedMagazineMotion>(output);
                if (motion == null) { motion = ScriptableObject.CreateInstance<Weapons.ImportedMagazineMotion>(); AssetDatabase.CreateAsset(motion, output); }
                motion.insertion = new Vector3[45];
                for (int i = 0; i < motion.insertion.Length; i++)
                {
                    clip.SampleAnimation(go, 2.2f * i / (motion.insertion.Length - 1));
                    var point = (reference * body.worldToLocalMatrix).MultiplyPoint3x4(magazine.bounds.center);
                    motion.insertion[i] = (point - seated) * scale;
                }
                motion.insertion[motion.insertion.Length - 1] = Vector3.zero;
                EditorUtility.SetDirty(motion);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void SetMap(Material mat, string property, string path, string keyword)
        {
            if (string.IsNullOrEmpty(path)) return;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Missing texture: " + path);
            mat.SetTexture(property, texture);
            if (keyword != null) mat.EnableKeyword(keyword);
        }

        static void MergeStatic(GameObject root, GameObject source, string output, string slug)
        {
            var groups = new Dictionary<string, Dictionary<Material, List<CombineInstance>>>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                string name = filter.name.ToLowerInvariant();
                string group = Guns.ContainsKey(slug) && (name == "mag" || name == "magazine" || name.StartsWith("mag")) ? "Magazine" : "Body";
                if (!groups.TryGetValue(group, out var materials)) groups[group] = materials = new Dictionary<Material, List<CombineInstance>>();
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    var mat = renderer.sharedMaterials[Mathf.Min(i, renderer.sharedMaterials.Length - 1)];
                    if (!materials.TryGetValue(mat, out var combines)) materials[mat] = combines = new List<CombineInstance>();
                    combines.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            foreach (var group in groups)
            {
                var submeshes = new List<Mesh>();
                foreach (var pair in group.Value)
                {
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    submeshes.Add(mesh);
                }
                var combined = new Mesh { name = slug + " " + group.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(submeshes.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, false);
                string path = output + "/" + slug + "_" + group.Key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(combined, path);
                else { existing.Clear(); EditorUtility.CopySerialized(combined, existing); UnityEngine.Object.DestroyImmediate(combined); combined = existing; }
                combined.UploadMeshData(false);
                EditorUtility.SetDirty(combined);
                var child = new GameObject(group.Key);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = combined;
                child.AddComponent<MeshRenderer>().sharedMaterials = group.Value.Keys.ToArray();
                foreach (var mesh in submeshes) UnityEngine.Object.DestroyImmediate(mesh);
            }
            UnityEngine.Object.DestroyImmediate(source);
        }

        static void RenderPreview(GameObject root, string path)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var copy = UnityEngine.Object.Instantiate(root);
                preview.AddSingleGO(copy);
                var b = BoundsOf(copy);
                preview.camera.transform.position = b.center + new Vector3(1.5f, 0.8f, 1.7f).normalized * b.size.magnitude * 1.4f;
                preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane = 0.001f;
                preview.camera.farClipPlane = 1000f;
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = b.size.magnitude * 0.65f;
                preview.camera.aspect = 1f;
                preview.camera.clearFlags = CameraClearFlags.Color;
                preview.camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, -40f, 0f);
                preview.lights[1].intensity = 0.8f;
                preview.ambientColor = new Color(0.4f, 0.4f, 0.4f);
                preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
                preview.Render();
                var image = preview.EndStaticPreview();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }

        static void MergeSkin(GameObject root, string path)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bones = new List<Transform>();
            var poses = new List<Matrix4x4>();
            var weights = new List<BoneWeight>();
            var combines = new List<CombineInstance>();
            var materials = new List<Material>();
            // One combine per source mesh (submeshes are preserved with mergeSubMeshes=false below).
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var triangles = new List<int[]>();
            foreach (var r in renderers)
            {
                var mesh = r.sharedMesh;
                var matrix = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                int offset = vertices.Count;
                vertices.AddRange(mesh.vertices.Select(matrix.MultiplyPoint3x4));
                normals.AddRange(mesh.normals.Select(v => matrix.inverse.transpose.MultiplyVector(v).normalized));
                uv.AddRange(mesh.uv);
                var indices = new int[r.bones.Length];
                for (int i = 0; i < r.bones.Length; i++)
                {
                    int index = bones.IndexOf(r.bones[i]);
                    if (index < 0) { index = bones.Count; bones.Add(r.bones[i]); poses.Add(mesh.bindposes[i] * matrix.inverse); }
                    indices[i] = index;
                }
                foreach (var source in mesh.boneWeights)
                {
                    var w = source;
                    w.boneIndex0 = indices[w.boneIndex0]; w.boneIndex1 = indices[w.boneIndex1];
                    w.boneIndex2 = indices[w.boneIndex2]; w.boneIndex3 = indices[w.boneIndex3];
                    weights.Add(w);
                }
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    triangles.Add(mesh.GetTriangles(i).Select(t => t + offset).ToArray());
                    materials.Add(r.sharedMaterials[i]);
                }
            }
            var combined = new Mesh { name = "Hazmat combined skin", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.SetVertices(vertices); combined.SetNormals(normals); combined.SetUVs(0, uv);
            combined.boneWeights = weights.ToArray(); combined.bindposes = poses.ToArray();
            combined.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) combined.SetTriangles(triangles[i], i);
            combined.RecalculateTangents(); combined.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(combined, path);
            else
            {
                // CopySerialized does not reliably transfer a generated skinned mesh's native
                // vertex buffers. It produced an asset with triangles/bind poses but zero vertices.
                existing.Clear();
                existing.indexFormat = combined.indexFormat;
                existing.vertices = combined.vertices;
                existing.normals = combined.normals;
                existing.uv = combined.uv;
                existing.tangents = combined.tangents;
                existing.bindposes = combined.bindposes;
                existing.boneWeights = combined.boneWeights;
                existing.subMeshCount = combined.subMeshCount;
                for (int i = 0; i < combined.subMeshCount; i++) existing.SetTriangles(combined.GetTriangles(i), i);
                existing.RecalculateBounds();
                UnityEngine.Object.DestroyImmediate(combined);
                combined = existing;
            }
            if (combined.vertexCount != vertices.Count || combined.boneWeights.Length != vertices.Count)
                throw new InvalidOperationException("Hazmat mesh lost its vertices or skin weights during generation.");
            combined.UploadMeshData(false);
            EditorUtility.SetDirty(combined);
            foreach (var r in renderers) UnityEngine.Object.DestroyImmediate(r);
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = combined; renderer.bones = bones.ToArray(); renderer.sharedMaterials = materials.ToArray();
            renderer.rootBone = bones.FirstOrDefault(t => t.name.EndsWith(":Hips"));
            renderer.localBounds = new Bounds(Vector3.up, new Vector3(3f, 3f, 3f));
            renderer.updateWhenOffscreen = true;
        }

        [MenuItem("Zombies FPS/Imported Models/Inspect sources")]
        public static void Inspect()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report = new StringBuilder();
            foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { Source }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                var go = UnityEngine.Object.Instantiate(asset);
                try
                {
                    var rs = go.GetComponentsInChildren<Renderer>();
                    Bounds bounds = rs.Length > 0 ? rs[0].bounds : new Bounds();
                    foreach (var r in rs) bounds.Encapsulate(r.bounds);
                    report.AppendLine(path + " bounds=" + bounds.ToString("F4"));
                    foreach (var r in rs) report.AppendLine("  " + r.name + " materials=" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "NULL" : m.name)));
                    foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                        report.AppendLine("  CLIP " + c.name + " duration=" + c.length + " frames=" + c.frameRate);
                    if (path.Contains("hazmat")) foreach (var t in go.GetComponentsInChildren<Transform>()) report.AppendLine("  BONE " + t.name);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            Directory.CreateDirectory("Temp/model-import");
            File.WriteAllText("Temp/model-import/inspection.txt", report.ToString());
        }
    }

    public sealed class ImportedModelSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/ImportedModels/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/ImportedModels/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileName(assetPath).ToLowerInvariant();
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            if (name.Contains("normal") || name.Contains("headnorm")) importer.textureType = TextureImporterType.NormalMap;
            if (name.EndsWith("_mask.png") || name.EndsWith("_ao.png")) importer.sRGBTexture = false;
            if (name.Contains("roughness") || name.Contains("metallic") || name.Contains("_ao") || name.Contains("gloss")) importer.sRGBTexture = false;
        }
    }
}
