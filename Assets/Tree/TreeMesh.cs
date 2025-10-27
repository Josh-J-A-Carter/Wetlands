using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(MeshFilter))]
public class TreeMesh : MonoBehaviour {

    [SerializeField]
    int pDepth = 10;

    [SerializeField]
    float pBranching = 2.25f, pAngle = 0.2f, pMaxWidth = 3.0f, pMaxLength = 20.0f, pDecrWidthFactor = 0.5f, pDecrLengthFactor = 0.25f;


    TreeSkeleton skeleton;

    const int BRANCH_RESOLUTION = 12;

    void Start() {
        skeleton = new(pDepth, pBranching, pAngle, pMaxLength, pMaxWidth, pDecrLengthFactor, pDecrWidthFactor);

        // Recurse through the tree skeleton, and add a new branch at each step.
        List<Vector3> vertices = new(){ Vector3.zero };
        List<int> triangles = new();

        List<Node> frontier = new() { skeleton.root };
        skeleton.root.index = 0;

        while (frontier.Count > 0) {

            List<Node> newFrontier = new();

            foreach (Node parent in frontier) {
                if (parent.children.Count == 0) continue;

                foreach (Node child in parent.children) {
                    newFrontier.Add(child);
                    (List<Vector3> deltaVertices, List<int> deltaTriangles) = GenerateMeshBranch(parent.pos, parent.width,
                                                    parent.index, child.pos, child.width, vertices.Count, BRANCH_RESOLUTION);
                    vertices.AddRange(deltaVertices);
                    triangles.AddRange(deltaTriangles);

                    child.index = vertices.Count - 1;
                }
            }

            frontier = newFrontier;
        }

        Mesh mesh = new Mesh {
            vertices = vertices.ToArray(),
            // uv = newUV,
            triangles = triangles.ToArray()
        };

        mesh.RecalculateNormals();
        
        // This assignment is temporary and will reset to the initial Mesh when exiting Play mode.
        GetComponent<MeshFilter>().mesh = mesh;
    }

    KeyValuePair<List<Vector3>, List<int>> GenerateMeshBranch(Vector3 v1, float w1, int v1Index, Vector3 v2, float w2,
            int startingIndex, int circleResolution) {
        Vector3 normal = Vector3.Normalize(v2 - v1);

        Vector3[] subspaceBasis = MeshUtility.FindPlaneBasis(normal);

        List<Vector3> ring1 = CalculateMeshBranchRing(v1, subspaceBasis, circleResolution, w1);
        List<Vector3> ring2 = CalculateMeshBranchRing(v2, subspaceBasis, circleResolution, w2);

        List<Vector3> vertices = ring1;
        vertices.AddRange(ring2);
        vertices.Add(v2);

        List<int> triangles = new();

        // Triangles between the two rings
        for (int i = 0 ; i < circleResolution; i += 1) {
            int lower0 = startingIndex + i;
            int upper0 = startingIndex + i + circleResolution;
            int lower1 = lower0 + 1;
            int upper1 = upper0 + 1;
            // Wrap back around at the final iteration
            if (i == circleResolution - 1) {
                lower1 = startingIndex;
                upper1 = startingIndex + circleResolution;
            }

            triangles.AddRange(new List<int>(){ lower0, upper0, lower1,     upper0, upper1, lower1 });
        }

        // Triangles between upper ring and v2
        for (int i = 0 ; i < circleResolution ; i += 1) {
            int upper0 = startingIndex + i + circleResolution;
            int upper1 = upper0 + 1;
            if (i == circleResolution - 1) upper1 = startingIndex + circleResolution;

            triangles.AddRange(new List<int>(){ startingIndex + 2 * circleResolution, upper1, upper0 });
        }

        // Triangles between lower ring and v1
        for (int i = 0 ; i < circleResolution ; i += 1) {
            int lower0 = startingIndex + i;
            int lower1 = lower0 + 1;
            if (i == circleResolution - 1) lower1 = startingIndex;

            triangles.AddRange(new List<int>(){ v1Index, lower0, lower1 });
        }

        return new(vertices, triangles);        
    }
    
    List<Vector3> CalculateMeshBranchRing(Vector3 centre, Vector3[] subspaceBasis, int numVerts, float radius) {
        // We first calculate where the points *would* go, if they were on the plane at the origin with normal (0, 1, 0)
        // These are the points (0, 0, 0), (0, 1 * Tau / numVerts, 0), (0, 2 * Tau / numVerts, 0)
        // We then translate these to the desired plane, using the map:
        // Phi(e1) = b1, Phi(e2) = b2           (b1 = subspaceBasis[0], b2 = subspaceBasis[1])
        // So if u = (a, b, 0), then u' = Phi(u) * w1 + v1

        List<Vector3> points = new();

        for (int i = 0 ; i < numVerts ; i += 1) {
            float theta = 2 * Mathf.PI * i / numVerts;
            Vector3 u1 = Mathf.Cos(theta) * subspaceBasis[0] + Mathf.Sin(theta) * subspaceBasis[1];
            Vector3 u2 = Vector3.Normalize(u1) * radius + centre;
            points.Add(u2);
        }

        return points;
    }
    
}