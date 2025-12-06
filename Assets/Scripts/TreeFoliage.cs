using System.Linq;
using UnityEngine;

public class TreeFoliage : MonoBehaviour {

    [SerializeField]
    TreeFoliageParameters param;

    Material[] materials;

    bool dying;

    bool initialised;


    float growthPercent = 0;
    float deathPercent = 0;

    void Start() {
        initialised = true;

        materials = GetComponentsInChildren<Renderer>().Select(r => r.material).ToArray();

        transform.localScale = new(param.startScale, param.startScale, param.startScale);
    }

    public void SetRotation(Vector3 forward, Vector3 upward) {
        transform.rotation = Quaternion.LookRotation(forward, upward);
    }

    public void SetPosition(Vector3 position) {
        transform.position = position;
    }

    public void Grow(float amount, Vector3 treeCentre) {
        if (!initialised) return;

        if (growthPercent >= 1) return;

        if (deathPercent > 0) return;

        growthPercent += amount;

        // Change scale here
        float scale = Lerp(param.startScale, param.endScale, growthPercent);
        transform.localScale = new(scale, scale, scale);

        // Change colour here
        foreach (Material mat in materials) {
            mat.SetColor("_Tint", GetCurrentColour());
            mat.SetVector("_TreeCentreWS", treeCentre);
        }
    }


    /// <summary>
    /// Increase death percentage by amount. Returns true if dead & ready for abscission, false otherwise
    /// </summary>
    public bool Die(float amount, Vector3 treeCentre) {
        deathPercent += amount;

        foreach (Material mat in materials) {
            mat.SetColor("_Tint", GetCurrentColour());
            mat.SetVector("_TreeCentreWS", treeCentre);
            mat.SetFloat("_Stipple", (deathPercent - param.stippleBeginThreshold) / (1.0f - param.stippleBeginThreshold));
        }

        return deathPercent >= 1;
    }

    public void BeginDeath() {
        dying = true;
    }

    public bool IsDying() {
        return dying;
    }

    Color GetCurrentColour() {
        return Lerp(Lerp(param.newGrowthColour, param.oldGrowthColour, growthPercent), param.deadGrowthColour, deathPercent);
    }

    Color Lerp(Color a, Color b, float t) {
        return a + t * (b - a);
    }

    float Lerp(float a, float b, float t) {
        return a + t * (b - a);
    }
}
