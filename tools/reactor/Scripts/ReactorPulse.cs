using UnityEngine;

/// <summary>Attach to imported Reactor_01 root. Works in Built-in, URP and HDRP.
/// Assign emissive materials using the included ReactorMaterialSetup editor utility.</summary>
public class ReactorPulse : MonoBehaviour
{
    [Header("Animation")]
    [Range(0f, 2f)] public float verticalDrift = 0.18f;
    [Range(0f, 8f)] public float pulseSpeed = 1.7f;
    [Range(0f, 1f)] public float pulseAmount = 0.25f;
    [Header("Optional scene light")]
    public Light reactorLight;
    public float baseLightIntensity = 4f;
    private Transform[] blobs;
    private Vector3[] origins;
    private Renderer[] glowRenderers;
    private MaterialPropertyBlock block;

    void Start()
    {
        var objects = GetComponentsInChildren<Transform>(true);
        var found = new System.Collections.Generic.List<Transform>();
        foreach (var t in objects) if (t.name.StartsWith("Plasma_Blob_")) found.Add(t);
        blobs = found.ToArray();
        origins = new Vector3[blobs.Length];
        for (int i = 0; i < blobs.Length; i++) origins[i] = blobs[i].localPosition;
        var emissive = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Plasma_") || r.name.StartsWith("Control_Status")) emissive.Add(r);
        glowRenderers = emissive.ToArray();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < blobs.Length; i++)
        {
            Vector3 p = origins[i];
            p.y += Mathf.Sin(t * (0.48f + i * 0.09f) + i * 1.6f) * verticalDrift;
            p.x += Mathf.Sin(t * .37f + i) * 0.04f;
            blobs[i].localPosition = p;
        }
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
        foreach (var r in glowRenderers)
        {
            r.GetPropertyBlock(block);
            Color emission = new Color(1f * pulse, .19f * pulse, .015f * pulse);
            block.SetColor("_EmissionColor", emission * 3f); // Built-in / URP
            block.SetColor("_EmissiveColor", emission * 3f); // HDRP
            r.SetPropertyBlock(block);
        }
        if (reactorLight) reactorLight.intensity = baseLightIntensity * pulse;
    }
}
