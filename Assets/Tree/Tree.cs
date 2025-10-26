using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;

public class Example : MonoBehaviour {
    [SerializeField] Vector3[] newVertices;
    [SerializeField] Vector2[] newUV;
    [SerializeField] int[] newTriangles;

    void Start() {

        (List<Vector3> vertices, List<int> triangles) = GenerateMeshBranch(new(0, 0, 0), 2, 0, new(0, 1, 0), 1, 1, 4);
        vertices.Insert(0, new(0, 0, 0));

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

        // Find a basis for the plane with this normal, which goes through the origin
        // (so that it's a subspace, not an affine subspace)
        Vector3[] standardBasis = { new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        Vector3[] crossVectors = new Vector3[3];
        for (int i = 0; i < 3; i += 1) crossVectors[i] = Vector3.Cross(standardBasis[i], normal);

        Vector3[] subspaceBasis = new Vector3[2];

        foreach (Vector3 v in crossVectors) {
            if (v == Vector3.zero) continue;

            subspaceBasis[0] = v;
            subspaceBasis[1] = Vector3.Cross(v, normal);
            break;
        }

        List<Vector3> ring1 = CalculateMeshBranchRing(v1, subspaceBasis, circleResolution, w1);
        List<Vector3> ring2 = CalculateMeshBranchRing(v2, subspaceBasis, circleResolution, w2);

        List<Vector3> vertices = ring1;
        vertices.AddRange(ring2);
        vertices.Add(v2);

        List<int> triangles = new();

        // Triangles between the two rings
        for (int i = 0; i < circleResolution; i += 1) {
            int lower0 = startingIndex + i;
            int upper0 = startingIndex + i + circleResolution;
            int lower1 = lower0 + 1;
            int upper1 = upper0 + 1;
            // Wrap back around at the final iteration
            if (i == circleResolution - 1) {
                lower1 = lower0 - i;
                upper1 = upper0 - i;
            }

            triangles.AddRange(new List<int>(){ lower0, upper0, lower1,     upper0, upper1, lower1});
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