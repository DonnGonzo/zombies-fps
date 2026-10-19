using UnityEngine;

public class DarkMagicAura : MonoBehaviour
{
    public bool startActive = false;
    public float rotationSpeed = 40f;
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 2.0f;
    public float pulseAmplitude = 0.12f;
    public float pulseSpeed = 2.5f;
    public Light auraLight;
    public float inactiveIntensity = 0f;
    public float activeIntensity = 4f;

    private Vector3 baseScale;
    private Vector3 basePosition;
    private bool isActive;

    void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.localPosition;
        SetActive(startActive);
    }

    void Update()
    {
        if (!isActive) return;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        transform.localScale = baseScale * pulse;
        transform.localPosition = basePosition + new Vector3(0f, Mathf.Sin(Time.time * bobSpeed) * bobAmplitude, 0f);

        if (auraLight != null)
            auraLight.intensity = activeIntensity + Mathf.Sin(Time.time * (pulseSpeed * 1.3f)) * 0.7f;
    }

    public void SetActive(bool value)
    {
        isActive = value;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.enabled = value;

        if (auraLight != null)
            auraLight.intensity = value ? activeIntensity : inactiveIntensity;
    }

    public void ActivateMoment() => SetActive(true);
    public void DeactivateMoment() => SetActive(false);
}
