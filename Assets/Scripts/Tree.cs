using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour {

    public const float GROWTH_TICK_DELAY = 0.005f;
    public const float GROWTH_TICK_INCR = 0.0001f;

    [SerializeField]
    TreeParameters currentParameters;
    
    public TreeBranch trunk { get; private set; }


    Mesh mesh;

    public void ResetTree() {
        trunk = new(this, Vector3.zero, Vector3.up, 0);
    }

    public void Start() {
        ResetTree();
        
        // Make the mesh
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;

        StartCoroutine(GrowthTick());
    }

    public TreeParameters CurrentParameters() {
        return currentParameters;
    }

    public Vector3 Origin() {
        return transform.position;
    }


    IEnumerator GrowthTick() {
        while (true) {
            // Calculate light absorbed
            float light = 1.0f;

            // Growth
            trunk.Grow(light, Vector3.zero, currentParameters);

            // Recursively add new side branches

            // Update the mesh
            TreeMesh tm = TreeMeshGenerator.Generate(this, MeshResolutionByDepth);

            mesh.Clear();

            mesh.vertices = tm.vertices.ToArray();
            mesh.triangles = tm.triangles.ToArray();

            mesh.RecalculateNormals();

            yield return new WaitForSeconds(GROWTH_TICK_DELAY);
        }
    }

    static int MeshResolutionByDepth(int depth) {
        if (depth <= 1) return 4;
        return 3;
    }
}


[Serializable]
public class TreeParameters {
    [SerializeField]
    public float apicalDominance = 0.5f;

    [SerializeField]
    public float growthSpeed = 1.0f;

    [SerializeField]
    public float minBranchingAngle = Mathf.PI / 4;

    [SerializeField]
    public float maxBranchingAngle = Mathf.PI / 4;

    [SerializeField]
    public float maxDirectionChangeAngle = Mathf.PI / 32;

    [SerializeField]
    public float widthToLenGrowthRatio = 0.05f;

    [SerializeField]
    public float internodeLength = 0.5f;

    [SerializeField]
    public int budDeletionDepth = 4;

    [SerializeField]
    public Phyllotaxy phyllotaxy = Phyllotaxy.Opposite;

    [SerializeField]
    public PhyllotaxyCycle phyllotaxyAngleCycle = PhyllotaxyCycle.Planar;

    [SerializeField]
    public BranchLengthByDepth branchLengthByDepth = new();

    [SerializeField]
    public float apexSplitAngle = Mathf.PI / 8;

    [SerializeField]
    public GameObject leavesPrefab;

    [SerializeField]
    public float leafToLengthGrowthRatio = 0.5f;

    [SerializeField]
    public int leafDeletionDepth = 3;

    [SerializeField]
    public float minLeafPairAngleDiff = Mathf.PI/3;

    [SerializeField]
    public float maxLeafPairAngleDiff = Mathf.PI;

    [SerializeField]
    public float terminalBranchLeafChance = 0.5f;

    public enum Phyllotaxy {
        Opposite,
        Alternate,
        Whorled
    }

    public enum PhyllotaxyCycle {
        Planar,
        Decussate,
        Spiral
    }

    [Serializable]
    public class BranchLengthByDepth {
        public int depth0 = 10;
        public int depth1 = 8;
        public int depth2 = 1;
        public int depth3 = 0;
        public int depth4 = 0;
    }
}
