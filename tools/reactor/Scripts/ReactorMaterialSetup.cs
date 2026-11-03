#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>Editor utility: select imported reactor instance in Hierarchy and run Tools > Reactor > Setup Materials.</summary>
public static class ReactorMaterialSetup
{
    [MenuItem("Tools/Reactor/Setup Materials on Selected Reactor")]
    public static void Setup()
    {
        GameObject root = Selection.activeGameObject;
        if (!root) { Debug.LogError("Select a Reactor_01 instance in the Hierarchy."); return; }
        string materialDir = "Assets/ReactorMaterials";
        if (!AssetDatabase.IsValidFolder(materialDir)) AssetDatabase.CreateFolder("Assets", "ReactorMaterials");
        var materials = new Dictionary<string, Material>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            string key = Classify(r.gameObject.name);
            if (!materials.TryGetValue(key, out Material mat))
            {
                string path = materialDir + "/" + key + ".mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (!shader) shader = Shader.Find("HDRP/Lit");
                    if (!shader) shader = Shader.Find("Standard");
                    mat = new Material(shader); mat.name = key;
                    Color c = GetColor(key);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (key == "Glass")
                    {
                        // Transparent rendering setup depends on pipeline: see README.
                        // Set alpha to 0.18 and choose Transparent surface in material Inspector.
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(.12f,.5f,.55f,.18f));
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(.12f,.5f,.55f,.18f));
                    }
                    if (key.StartsWith("Plasma") || key == "IndicatorCyan")
                    {
                        mat.EnableKeyword("_EMISSION");
                        Color e = key == "IndicatorCyan" ? new Color(.03f,.7f,1f) : new Color(1f,.2f,.015f);
                        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", e * 3f);
                        if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", e * 3f);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    AssetDatabase.CreateAsset(mat, path);
                }
                materials[key] = mat;
            }
            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Reactor materials created/assigned. Set Glass surface to Transparent manually and add Bloom.");
    }
    static string Classify(string n)
    {
        if (n.StartsWith("Glass_")) return "Glass";
        if (n.StartsWith("Plasma_Blob")) return "PlasmaGold";
        if (n.StartsWith("Plasma_") || n.StartsWith("Control_Status")) return "PlasmaOrange";
        if (n.StartsWith("Control_Screen")) return "IndicatorCyan";
        if (n.StartsWith("Warning_")) return "WarningYellow";
        if (n.StartsWith("Copper_") || n.StartsWith("Bright_Gasket")) return "Copper";
        if (n.StartsWith("Rivet_") || n.Contains("Socket") || n.Contains("Cap")) return "ScratchedSteel";
        if (n.StartsWith("Foundation_Base") || n.StartsWith("Ceiling_Mount") || n.Contains("Support") || n.Contains("Mount")) return "DarkSteel";
        if (n.Contains("Ring") || n.Contains("Brace") || n.Contains("Key")) return "Gunmetal";
        return "CharcoalMetal";
    }
    static Color GetColor(string k)
    {
        switch (k)
        {
            case "Glass": return new Color(.12f,.5f,.55f,.18f);
            case "PlasmaGold": return new Color(1f,.56f,.045f);
            case "PlasmaOrange": return new Color(1f,.15f,.015f);
            case "IndicatorCyan": return new Color(.03f,.75f,.9f);
            case "WarningYellow": return new Color(.85f,.6f,.12f);
            case "Copper": return new Color(.45f,.23f,.09f);
            case "ScratchedSteel": return new Color(.42f,.45f,.46f);
            case "DarkSteel": return new Color(.085f,.105f,.12f);
            case "Gunmetal": return new Color(.23f,.26f,.28f);
            default: return new Color(.14f,.16f,.18f);
        }
    }
}
#endif
