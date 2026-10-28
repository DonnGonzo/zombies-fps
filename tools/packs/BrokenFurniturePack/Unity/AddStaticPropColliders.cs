using UnityEngine;

/// <summary>Creates a simple bounding-box collider on a static prop root.
/// Attach to the imported prop root, then choose Add Colliders from component context menu.</summary>
public class AddStaticPropColliders : MonoBehaviour
{
    [ContextMenu("Add Bounds Box Collider")]
    public void AddBoundsBoxCollider()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
        BoxCollider box = GetComponent<BoxCollider>();
        if (!box) box = gameObject.AddComponent<BoxCollider>();
        box.center = localCenter;
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }
}
