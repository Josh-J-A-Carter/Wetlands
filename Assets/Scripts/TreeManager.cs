using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeManager : MonoBehaviour {
    static TreeManager instance;

    public const float TICK_DELAY = 0.005f;

    [SerializeField]
    List<Tree> trees;


    Grid3D grid;

    [SerializeField]

    int gridX, gridY, gridZ, gridDensity;

    [SerializeField]
    Vector3 gridOrigin;

    void Awake() {
        if (instance) {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start() {
        StartCoroutine(Tick());

        grid = new(gridOrigin, gridX, gridY, gridZ, gridDensity);
    }

    public void Update() {
        grid.DrawGizmos();
    }

    public static Grid3D Grid() {
        return instance.grid;
    }

    IEnumerator Tick() {
        while (true) {
            grid = new(gridOrigin, gridX, gridY, gridZ, gridDensity);

            foreach (Tree t in trees) {
                t.GrowthTick();
                t.UpdateGrid(grid);
            }

            yield return new WaitForSeconds(TICK_DELAY);
        }
    }
}