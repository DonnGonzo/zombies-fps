using UnityEngine;

[RequireComponent(typeof(Light))]
public class RitualFlicker : MonoBehaviour
{
    public float baseIntensity = 1.8f;
    public float flickerAmount = 0.45f;
    public float speed = 10f;
    private Light lit;

    void Awake() => lit = GetComponent<Light>();

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * speed, 0.13f);
        lit.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
    }
}
