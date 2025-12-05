using UnityEngine;

[CreateAssetMenu(fileName = "Tree Foliage Parameters", menuName = "Scriptable Objects")]
public class TreeFoliageParameters : ScriptableObject {
    [field: SerializeField]
    public float startScale { get; private set; } = 0.005f;
    [field: SerializeField]
    public float endScale { get; private set; } = 0.05f;

    [field: SerializeField]
    public Color oldGrowthColour { get; private set; } = Color.green;

    [field: SerializeField]
    public Color newGrowthColour { get; private set; } = Color.lightGreen;

    [field: SerializeField]
    public Color deadGrowthColour { get; private set; } = Color.orangeRed;
}
