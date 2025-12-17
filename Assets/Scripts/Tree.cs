using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour {

    public const float GROWTH_TICK_INCR = 0.0001f;

    [SerializeField]
    TreeParameters currentParameters;
    
    public TreeBranch trunk { get; private set; }


    Mesh mesh;

    public void ResetTree() {
        trunk = new(this, null, transform.position, Vector3.up, 0);

        TreeFoliage[] foliage = GetComponentsInChildren<TreeFoliage>();
        foreach (TreeFoliage f in foliage) Destroy(f.gameObject);
    }

    public void Awake() {
        ResetTree();
        
        // Make the mesh
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    public TreeParameters CurrentParameters() {
        return currentParameters;
    }

    public Vector3 Origin() {
        return transform.position;
    }


    public void GrowthTick() {
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
    }

    static int MeshResolutionByDepth(int depth) {
        if (depth <= 1) return 4;
        return 3;
    }

    public void UpdateGrid(Grid3D grid) {
        List<TreeBranch> currentIteration = new() { trunk };

        while (currentIteration.Count > 0) {
            List<TreeBranch> nextIteration = new();

            foreach (TreeBranch branch in currentIteration) {
                if (branch.GetDepth() > 1) continue;

                for (int i = 0 ; i < branch.NodeCount() - 1 ; i += 1) {
                    TreeNode node1 = branch.GetNode(i).Item1;
                    TreeNode node2 = branch.GetNode(i + 1).Item1;

                    Vector3 p = node1.positionWorld;
                    Vector3 dir = node2.positionWorld - node1.positionWorld;
                    float length = dir.magnitude;
                    float radius = node1.width;
                    GridSet ray = grid.CastRayWorldSpace(p, dir, radius, length);
                    grid.SetOccupied(ray);
                }

                foreach (TreeBranch child in branch.GetAllSideBranches()) nextIteration.Add(child);
            }

            currentIteration = nextIteration;
        }
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
