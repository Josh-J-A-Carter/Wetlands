using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour {

    TreeParameters currentParameters;
    
    public TreeBranch trunk { get; private set; }

    List<GizmoData> gizmos;

    Mesh mesh;

    public void Start() {
        trunk = new();
        trunk.TestAddNodes(new() {
            new(Vector3.zero, 2.0f),
            new(new(0, 2, 0), 1.5f),
        //     new(new(0, 4, 0), 1.0f),
        //     new(new(0, 6, 0), 0.5f)
        // }, new(new(0, 8, 0), 0.25f), new(0, 1.0f, 0));
        }, new(new(0, 6, 0), 0.5f), new(0, 1.0f, 0));


        TreeBranch sidebranch = new();
        sidebranch.TestAddNodes(new() {
            new(new(-1.1f, 2.0f, 1.1f), 0.25f),
            new(new(-1.75f, 4.0f, 1.75f), 0.2f),
            new(new(-2.25f, 5.5f, 2.25f), 0.15f)
        }, new(new(-2.75f, 7.0f, 2.75f), 0.1f), new(-1.0f, 3.0f, 1.0f));

        trunk.TestAddBranch(sidebranch, 1);
        
        // Make the mesh
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;

        (List<Vector3> vertices, List<int> triangles, List<GizmoData> gizmos) = TreeMeshGenerator.Generate(this, 4);
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        this.gizmos = gizmos;

        mesh.RecalculateNormals();
    }


    private void OnDrawGizmos() {

        if (gizmos == null) return;

        foreach (GizmoData g in gizmos) {
            Gizmos.color = g.col;

            if (g.end != Vector3.zero) {
                Gizmos.DrawLine(g.start, g.end);
                Gizmos.DrawSphere(g.end, 0.05f);
            }

            else Gizmos.DrawSphere(g.start, 0.05f);
        }
    }


}


public class TreeParameters {
    public float apicalDominance { get; private set; }
}
