using System.Collections.Generic;
using UnityEngine;

public class TreeSkeleton {

    public Node root { get; private set; }

    const int MAX_DEPTH_UNDERSHOOT = 2;
    const int MAX_DEPTH_OVERSHOOT = 2;

    const float MAX_ANGLE_UNDERSHOOT = 3.14f / 6.0f;
    const float MAX_ANGLE_OVERSHOOT = 3.14f / 6.0f;

    int pDepth;
    float pBranching;
    float pAngle;
    float pMaxLength;
    float pMaxWidth;
    float pDownsizeFactor;

    public TreeSkeleton(int pDepth, float pBranching, float pAngle, float pMaxLength, float pMaxWidth, float pDownsizeFactor) {
        this.pDepth = pDepth;
        this.pBranching = pBranching;
        this.pAngle = pAngle;
        this.pMaxLength = pMaxLength;
        this.pMaxWidth = pMaxWidth;
        this.pDownsizeFactor = pDownsizeFactor;

        root = new(Vector3.zero, null, pMaxWidth);
        List<Node> frontier = new() { root };

        int depth = 1;
        int maxDepth = Mathf.Max(Random.Range(pDepth - MAX_DEPTH_UNDERSHOOT, pDepth + MAX_DEPTH_OVERSHOOT), 3);

        while (frontier.Count > 0 && depth < maxDepth) {
            List<Node> newFrontier = new();

            foreach (Node parent in frontier) {
                // How many branches to attach here?
                int branches = GenNumBranches(depth);
                // Find a basis for the 2D subspace with normal = parent.ThroughLine()
                Vector3 normal = parent.ThroughLine();
                Vector3[] subspaceBasis = MeshUtility.FindPlaneBasis(normal);

                for (int i = 0 ; i < branches ; i += 1) {
                    Vector3 pos = GenDirection(depth, subspaceBasis, normal, branches, i) + parent.pos;
                    float width = GenWidth(depth);
                    Node child = new(pos, parent, width);

                    parent.children.Add(child);
                    newFrontier.Add(child);
                }
            }

            frontier = newFrontier;
            depth += 1;
        }
    }

    int GenNumBranches(float currDepth) {
        if (currDepth == 1) return 1;

        int b = Mathf.RoundToInt(Random.Range(pBranching - 1, pBranching + 1));
        if (b < 1 && currDepth < pDepth - MAX_DEPTH_UNDERSHOOT) b = 1;
        if (b > 4) b = 4;
        return b;
    }

    Vector3 GenDirection(float currDepth, Vector3[] subspaceBasis, Vector3 normal, int branches, int iteration) {
        // If this is the first branch, it should be going up.
        if (currDepth == 1) return Mathf.Exp(- pDownsizeFactor * (currDepth - 1)) * pMaxLength * Vector3.up;

        float theta = 2 * Mathf.PI * iteration / branches;
        // This is where the desired direction should be when it is projected onto the plane.
        Vector3 dirProjected = Mathf.Cos(theta) * subspaceBasis[0] + Mathf.Sin(theta) * subspaceBasis[1];
        dirProjected = dirProjected.normalized;
        // Now, we reconstruct the desired branch direction, so that dir makes an angle phi ~ pAngle with normal
        float phi = Random.Range(pAngle - MAX_ANGLE_UNDERSHOOT, pAngle + MAX_ANGLE_OVERSHOOT);

        Vector3 dir = Mathf.Cos(phi) * normal + Mathf.Sin(phi) * dirProjected;

        // Then, we scale the direction to the appropriate length
        
        // currDepth - 1, since we start looping with depth = 1
        // We want the length multiplier to be 1 for the first iteration, but Exp(0) = 1
        return Mathf.Exp(- pDownsizeFactor * (currDepth - 1)) * pMaxLength * dir.normalized;
    }

    float GenWidth(float currDepth) {
        return pMaxWidth * Mathf.Exp(- pDownsizeFactor * currDepth);
    }

}

public class Node {
    public Vector3 pos { get; private set; }
    public Node parent { get; private set; }
    public float width { get; private set; }
    public List<Node> children { get; private set; }

    public int index;

    public Node(Vector3 pos, Node parent, float width) {
        this.pos = pos;
        this.parent = parent;
        this.width = width;

        children = new();
    }

    public Vector3 ThroughLine() {
        if (parent == null) return Vector3.up;

        return (pos - parent.pos).normalized;
    }
}
