using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(MeshFilter))]
public class TreeMesh : MonoBehaviour {

    [SerializeField]
    int pDepth = 10;

    [SerializeField]
    float pBranching = 2.25f, pAngle = 0.2f, pMaxWidth = 3.0f, pMaxLength = 20.0f, pDecrWidthFactor = 0.5f, pDecrLengthFactor = 0.25f;

    TreeSkeleton skeleton;

    const int MAX_BRANCH_RESOLUTION = 12;
    const int MIN_BRANCH_RESOLUTION = 4;

    Mesh mesh;


    int growthStage = 0;
    float growthStageProgress = 0.0f;
    bool growthComplete = false;

    const float GROWTH_PROGRESS_INCREMENT = 0.00625f;

    const float GROWTH_PROGRESS_DELAY = 0.1f;

    void Start() {
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;

        ResetTree();
    }

    void ResetTree() {
        skeleton = new(pDepth, pBranching, pAngle, pMaxLength, pMaxWidth, pDecrLengthFactor, pDecrWidthFactor);
        growthStage = 0;
        growthStageProgress = 0.0f;
        growthComplete = false;

        RegenerateMesh();

        StartCoroutine(GrowTick());        
    }

    void Update() {
        if (Keyboard.current.eKey.wasPressedThisFrame) {
            ResetTree();
        }
    }

    IEnumerator GrowTick() {
        while (growthComplete == false) {
            yield return new WaitForSeconds(GROWTH_PROGRESS_DELAY);
            Grow();
        }
    }

    void Grow() {
        if (growthComplete) return;

        growthStageProgress += GROWTH_PROGRESS_INCREMENT;

        if (growthStageProgress >= 1.0f) {
            growthStage += 1;
            growthStageProgress = 0.0f;
            if (growthStage >= skeleton.depth) growthComplete = true;
        }

        RegenerateMesh();
    }

    float GetGrowthFactor() {
        if (growthComplete) return 1.0f;

        //
        // We have depth - 1 branches, so depth - 1 branches (starting from 0)
        // so we can allocate 1 / depth growth factor at each stage
        // At stage 0, we have (0 + progress) / depth  growth
        // At stage 1, we have (1 + progress) / depth
        // ...
        // Once complete, we can't rely on 'progress' being correctly set, but growth is 1.0 (maximal)
        return (growthStage + growthStageProgress) / skeleton.depth;
    }


    void RegenerateMesh() {
        // Recurse through the tree skeleton, and add a new branch at each step.
        List<Vector3> vertices = new(){ Vector3.zero };
        List<Vector2> uv2 = new() { ComputeUV2(0, false) };
        List<int> triangles = new();

        List<Node> frontier = new() { skeleton.root };
        skeleton.root.index = 0;
        int depth = 0;

        while (frontier.Count > 0) {

            List<Node> newFrontier = new();

            foreach (Node parent in frontier) {
                if (parent.children.Count == 0) continue;

                foreach (Node child in parent.children) {
                    int resolution = MAX_BRANCH_RESOLUTION;
                    if (depth >= 3) resolution = (MAX_BRANCH_RESOLUTION + MIN_BRANCH_RESOLUTION) / 2;
                    if (depth >= 5) resolution = MIN_BRANCH_RESOLUTION;

                    // Has the tree fully grown up to this point?
                    // If not, we need to pick an intermediate point on the branch
                    Vector3 targetPos = child.pos;
                    float targetWidth = child.width;
                    if (depth == growthStage) {
                        targetPos = parent.pos + (child.pos - parent.pos) * growthStageProgress;
                        targetWidth *= Mathf.Exp(2 * (growthStageProgress - 1));
                    }

                    (List<Vector3> deltaVertices, List<int> deltaTriangles) = GenerateMeshBranch(parent.pos, parent.width,
                                                    parent.index, targetPos, targetWidth, vertices.Count, resolution);
                    vertices.AddRange(deltaVertices);
                    triangles.AddRange(deltaTriangles);

                    // Update the UV2 coordinates, with respect to the upper and lower rings of the cylinder
                    // Upper ring contains an extra vertex
                    Vector2 uvLower = ComputeUV2(depth, false);
                    Vector2 uvUpper = ComputeUV2(depth, true);
                    uv2.AddRange(Enumerable.Repeat(uvLower, resolution));
                    uv2.AddRange(Enumerable.Repeat(uvUpper, resolution + 1));

                    child.index = vertices.Count - 1;
                    newFrontier.Add(child);
                }
            }

            // Tree hasn't grown path this depth yet
            if (depth == growthStage) break;

            frontier = newFrontier;
            depth += 1;
        }

        // Scale the entire tree according to its age.
        float growthFactor = GetGrowthFactor();
        for (int i = 0 ; i < vertices.Count ; i += 1) vertices[i] = vertices[i] * growthFactor;

        // Refresh the mesh
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv2 = uv2.ToArray();
        mesh.RecalculateNormals();        
    }

    Vector2 ComputeUV2(int currDepth, bool upperRing) {
        if (currDepth < growthStage - 2) return new(0, 0);

        if (currDepth == growthStage - 2) {
            if (upperRing) return new(1 - growthStageProgress, 0);
            return new(0, 0);
        }

        if (currDepth == growthStage - 1) {
            if (upperRing) return new(1, 0);
            return new(1 - growthStageProgress, 0);
        }

        return new(1, 0);
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