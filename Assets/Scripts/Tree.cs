using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour {

    public const float GROWTH_TICK_DELAY = 0.005f;
    public const float GROWTH_TICK_INCR = 0.0001f;

    [SerializeField]
    TreeParameters currentParameters;
    
    public TreeBranch trunk { get; private set; }

    List<GizmoData> gizmos;

    Mesh mesh;

    public void Start() {
        trunk = new(Vector3.zero, Vector3.up, 0);
        
        // Make the mesh
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;

        StartCoroutine(GrowthTick());
    }


    private void OnDrawGizmos() {

        if (gizmos == null) return;

        foreach (GizmoData g in gizmos) {
            Gizmos.color = g.col;

            if (g.end != Vector3.zero) {
                Gizmos.DrawLine(g.start, g.end);
                Gizmos.DrawSphere(g.end, 0.01f);
            }

            else Gizmos.DrawSphere(g.start, 0.01f);
        }
    }


    IEnumerator GrowthTick() {
        while (true) {
            // Calculate light absorbed
            float light = 1.0f;

            // Growth
            this.gizmos = new();
            trunk.Grow(light, Vector3.zero, currentParameters);

            // Recursively add new side branches

            // Update the mesh
            (List<Vector3> vertices, List<int> triangles, List<GizmoData> gizmos) = TreeMeshGenerator.Generate(this, 4);
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            this.gizmos.AddRange(gizmos);

            mesh.RecalculateNormals();

            yield return new WaitForSeconds(GROWTH_TICK_DELAY);
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
    public float branchingAngle = Mathf.PI / 4;

    [SerializeField]
    public float widthToLenGrowthRatio = 0.05f;

    [SerializeField]
    public float internodeLength = 0.5f;

    [SerializeField]
    public Phyllotaxy phyllotaxy = Phyllotaxy.Opposite;

    public enum Phyllotaxy {
        Opposite,
        Alternate,
        Whorled
    }
}
