using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour {

    TreeParameters currentParameters;
    
    public TreeBranch trunk { get; private set; }

    Mesh mesh;

    public void Start() {
        trunk = new();
        trunk.TestAddNodes(new() {
            new(Vector3.zero, 1.0f),
            new(new(0, 1, 0), 0.75f),
            new(new(0, 2, 0), 0.5f),
            new(new(0, 3, 0), 0.25f)
    }, new(new(0, 4, 0), 0.2f), new(0, 1.0f, 0));

        TreeBranch sidebranch = new();
        trunk.TestAddNodes(new() {
            new(new(0.5f, 2, 0), 0.25f),
            new(new(1.0f, 2.5f, 0), 0.2f),
            new(new(1.5f, 3.0f, 0), 0.15f)
        }, new(new(2.0f, 3.5f, 0), 0.1f), new(1.0f, 1.0f, 0));

        trunk.TestAddBranch(sidebranch, 2);
        
        // Make the mesh
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;

        Tuple<List<Vector3>, List<int>> output = TreeMeshGenerator.Generate(this, 4);
        mesh.vertices = output.Item1.ToArray();
        mesh.triangles = output.Item2.ToArray();

        mesh.RecalculateNormals();
    }


}


public class TreeParameters {
    public float apicalDominance { get; private set; }
}
