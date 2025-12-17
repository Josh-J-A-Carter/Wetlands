using UnityEngine;

[RequireComponent(typeof(Light))]
public class SunLight : MonoBehaviour {

    new Light light;

    [SerializeField]
    float maxIntensity = 2;

    /// <summary>
    /// Length of day/night cycle in seconds
    /// </summary>
    [SerializeField]
    float cycleLength = 60.0f;

    void Start() {
        light = GetComponent<Light>();
    }

    public Vector3 LightDirection() {
        return transform.forward;
    }

    void Update() {
        // Repeat every phi = 2*n*pi
        // Want to repeat every phi' = k * 

        transform.forward = MeshUtility.Sphere(0, Time.time * Mathf.PI * 2 / cycleLength);

        light.intensity = Mathf.Clamp01(Vector3.Dot(-LightDirection(), Vector3.up)) * maxIntensity;
    }
}