using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;

public static class TreeMeshGenerator {

    const int BRANCH_BASE_RESOLUTION = 4;

    class TreeMeshGeneratorState {
        public Tree tree;
        public List<Vector3> vertices;
        public List<int> triangles;

        public List<GizmoData> gizmos;

        public int branchRingResolution;

        public TreeMeshGeneratorState(Tree tree, int branchRingResolution) {
            this.tree = tree;
            this.branchRingResolution = branchRingResolution;

            vertices = new();
            triangles = new();
            gizmos = new();
        }
    }
    
    public static Tuple<List<Vector3>, List<int>, List<GizmoData>> Generate(Tree tree, int branchRingResolution) {
        TreeMeshGeneratorState state = new(tree, branchRingResolution);

        // Recursive branch generation algorithm
        GenerateBranch(state, tree.trunk, null, null);
        
        return new(state.vertices, state.triangles, state.gizmos);
    }

    static void GenerateBranch(TreeMeshGeneratorState state, TreeBranch branch, TreeMeshNode branchBase, PlaneOrthoBasis previousBasis) {

        // Create the tentative mesh structure
            // If this branch is already a side branch, we need to use its parent's basis to inform this one's
            // Otherwise, the connections between branches could become twisted
        List<TreeMeshNodeRing> branchMeshStructure = BranchMeshStructure(state, branch, previousBasis);

        // Resolve side branches, and recursively generate their mesh

        // 
        // TO DO: Allow branches to supersede polygon vertex TreeMeshNodes
        // The projections become more complicated as there are four planes to consider
        // 

        for (int nodeIndex = 0 ; nodeIndex < branch.NodeCount() ; nodeIndex += 1) {
            List<TreeBranch> branches = branch.GetSideBranchesAt(nodeIndex);
            (TreeNode n, Vector3 nNormal) = branch.GetNode(nodeIndex);
            PlaneOrthoBasis nBasis = MeshUtility.PlaneOrthoBasis(nNormal);
            if (previousBasis != null) nBasis = MeshUtility.PlaneOrthoBasis(nNormal, previousBasis.v1, previousBasis.v2);

            foreach (TreeBranch sideBranch in branches) {
                // Side branches cannot occur at node 0; this would be either in the soil, 
                // or at the connection point of two branches.
                // Similarly, branches do not occur at the terminal bud, i.e. index branch.NodeCount() - 1.
                Assert.IsTrue(nodeIndex != 0);
                Assert.IsTrue(nodeIndex != branch.NodeCount() - 1);

                (TreeMeshNode attachmentNode, PlaneOrthoBasis nPrimeBasis) = 
                        CalculateSideBranchAttachment(state, sideBranch, branchMeshStructure[nodeIndex], n, nBasis);

                // state.gizmos.Add(new(p0, preProjectionPoints[0], Color.red));
                // state.gizmos.Add(new(p1, preProjectionPoints[1], Color.green));
                // state.gizmos.Add(new(p2, preProjectionPoints[2], Color.blue));
                // state.gizmos.Add(new(p3, preProjectionPoints[3], Color.white));

                // Recurse on the branch
                GenerateBranch(state, sideBranch, attachmentNode, nPrimeBasis);
            }
        }

        // Confirm any remaining indeterminate mesh nodes as simple nodes
        foreach (TreeMeshNodeRing r in branchMeshStructure) {
            TreeMeshNode initial = r.GetPolygonNode(0);
            TreeMeshNode current = initial;

            do {
                if (current.type == TreeMeshNodeType.Indeterminate) current.ConfirmSimple(state);
                current = current.neighbourRight;
            } while (current != initial);
        }

        // Create triangles

        // 1. Connect branchBase to branchMeshStructure (if branchBase exists)

        if (branchBase != null) ConnectSideBranch(state, branchBase, branchMeshStructure);

        // 2. Main connections
        // Moving frame of 4 mesh nodes
        TreeMeshNode n00 = branchMeshStructure[0].GetPolygonNode(0);
        TreeMeshNode n10 = n00.neighbourUp;
        TreeMeshNode n01 = n00.neighbourRight;
        TreeMeshNode n11 = n01.neighbourUp;

        // Stop when we get to the topmost ring, i.e. n00 is at the terminal bud
        while (n10 != null) {
            
            TreeMeshNode initial = n00;

            do {
                ConnectMeshNodePanel(state, n00, n10, n01, n11);

                // Move around this current ring
                n00 = n01;
                n10 = n11;
                n01 = n01.neighbourRight;
                n11 = n11.neighbourRight;
            } while (n00 != initial);

            // Move up one ring
            n00 = n00.neighbourUp;
            n10 = n00.neighbourUp;
            n01 = n00.neighbourRight;
            n11 = n01.neighbourUp;
        }

        // 3. Close up the top

        // 
        // TO DO
        // 

    }

    static Tuple<TreeMeshNode, PlaneOrthoBasis> CalculateSideBranchAttachment(TreeMeshGeneratorState state, TreeBranch sideBranch,
                        TreeMeshNodeRing attachmentRing, TreeNode attachmentNode, PlaneOrthoBasis attachmentBasis) {
        // Determine which mesh nodes to insert this between
        (TreeNode nPrime, Vector3 nPrimeNormal) = sideBranch.GetNode(0);
        float theta = MeshUtility.PolygonVertexToAngle(nPrime.position, attachmentBasis, attachmentNode.position);

        // We have (2 * PI / n) * i =< theta =< (2 * PI / n) * (i + 1) for some integer i in [0, resolution - 1]
        // i =< theta * n / (2 * PI) =< i + 1
        // So i = floor(theta * n / (2 * PI))
        int vertexIndex = Mathf.FloorToInt(theta * sideBranch.NodeCount() / (2 * Mathf.PI));

        TreeMeshNode left = attachmentRing.GetPolygonNode(vertexIndex);
        TreeMeshNode right = attachmentRing.GetPolygonNode(vertexIndex + 1);

        // Find the polygon (square) at the base of the side branch
        // and map this into the upper and lower planes on the main branch
        Vector3 lineDir = right.position - left.position;
        Vector3 upDir = left.neighbourUp.position - left.position;
        PlaneOrthoBasis nPrimeBasis = MeshUtility.PlaneOrthoBasis(nPrimeNormal, lineDir, upDir);
        List<Vector3> preProjectionPoints = MeshUtility.ConstructRegularPolygon(nPrimeBasis, nPrime.position,
                                                                        nPrime.width, BRANCH_BASE_RESOLUTION);
        
        // Assume that the base resolution is 4, for the purposes of the following code.
        Assert.IsTrue(BRANCH_BASE_RESOLUTION == 4);

        // centre, p0 & p2 are projected onto the line between left and right
        Vector3 centre = MeshUtility.ProjectOntoLine(nPrime.position, lineDir, left.position);
        Vector3 p1 = MeshUtility.ProjectOntoLine(preProjectionPoints[1], lineDir, left.position);
        Vector3 p3 = MeshUtility.ProjectOntoLine(preProjectionPoints[3], lineDir, left.position);

        // p1 is projected onto the plane that passes through left, right, and left.neighbourUp.
        // left.neighbourUp is guaranteed to exist since we assumed this is not the terminal bud.
        Vector3 upNormal = MeshUtility.ComputePlaneNormal(left.position, right.position, left.neighbourUp.position);
        Vector3 p0 = MeshUtility.ProjectOntoPlane(preProjectionPoints[0], upNormal, left.position);

        // Similar process for p3; we already assumed that left.neighbourDown exists
        Vector3 downNormal = MeshUtility.ComputePlaneNormal(left.position, right.position, left.neighbourDown.position);
        Vector3 p2 = MeshUtility.ProjectOntoPlane(preProjectionPoints[2], downNormal, left.position);

        // Update the intermediate mesh node between left and right
        left.neighbourRight.ConfirmBranch(state, centre, p0, p2, p1, p3);
        return new(left.neighbourRight, nPrimeBasis);
    }

    static List<TreeMeshNodeRing> BranchMeshStructure(TreeMeshGeneratorState state, TreeBranch branch, PlaneOrthoBasis previousBasis) {
        List<TreeMeshNodeRing> rings = new();

        TreeMeshNodeRing prev = null;
        for (int i = 0 ; i < branch.NodeCount() ; i += 1) {
            (TreeNode n, Vector3 direction) = branch.GetNode(i);

            TreeMeshNodeRing ring = new(n, direction, state.branchRingResolution, prev, previousBasis);
            rings.Add(ring);
            prev = ring;
        }

        return rings;
    }

    static void ConnectMeshNodePanel(TreeMeshGeneratorState state, TreeMeshNode n00, TreeMeshNode n10,
                                                                    TreeMeshNode n01, TreeMeshNode n11) {
        
        List<int> indices = new() {
            n10.vertexDown, n10.vertexRight, // n10
            n11.vertexLeft, n11.vertexDown, // n11
            n01.vertexUp, n01.vertexLeft, // n01
            n00.vertexRight // back to n00
        };


        for (int tri = 0 ; tri < indices.Count - 1; tri += 1) {
            int v0 = n00.vertexUp;
            int v1 = indices[tri];
            int v2 = indices[tri + 1];

            // Indices will sometimes be equal due to simple nodes (they only have one vertex)
            if (v0 == v1 || v0 == v2 || v1 == v2) continue;

            AddTriangle(state, v0, v1, v2);
        }
    }

    static void ConnectSideBranch(TreeMeshGeneratorState state, TreeMeshNode branchBase, List<TreeMeshNodeRing> branchMeshStructure) {
        // Assume that the base resolution is 4, for the purposes of the following code.
        Assert.IsTrue(BRANCH_BASE_RESOLUTION == 4);

        // Let b refer to branchBase, v refer to vertices of ring 0 in branchMeshStructure, and n be the resolution of v.
        // For the left half of the shape, we construct:
        // p1 + p0 + v0, p1 + v0 + v1, p1 + v1 + v2, ... , p1 + v(floor(n/2) - 1) + v(floor(n/2)), p1 + v(floor(n/2)) + p2

        // If n is odd, also add triangles for the middle with p2

        // For the right half of the shape, we construct triangles in a similar fashion to the left side.

        int p0 = branchBase.vertexUp;
        int p1 = branchBase.vertexLeft;
        int p2 = branchBase.vertexDown;
        int p3 = branchBase.vertexRight;

        TreeMeshNodeRing ring0 = branchMeshStructure[0];
        int n = ring0.Resolution();

        // Left side
        List<int> leftIndices = new();
        int leftMax = Mathf.FloorToInt(n / 2);
        for (int i = 0 ; i <= leftMax ; i += 1) {
            TreeMeshNode vert = ring0.GetPolygonNode(i);
            leftIndices.Add(vert.vertexDown);
            // Need to include intermediate nodes - apart from the last vertex's right neighbour,
            // since this is too far from p1
            if (i < leftMax) leftIndices.Add(vert.neighbourRight.vertexDown);
        }

        leftIndices.Insert(0, p1);
        leftIndices.Add(p3);

        for (int tri = 0 ; tri < leftIndices.Count - 1 ; tri += 1) AddTriangle(state, p2, leftIndices[tri], leftIndices[tri + 1]);

        // Right side
        List<int> rightIndices = new();
        int rightMin = Mathf.CeilToInt(n / 2);
        for (int i = rightMin ; i <= n ; i += 1) {
            TreeMeshNode vert = ring0.GetPolygonNode(i);
            rightIndices.Add(vert.vertexDown);
            // Need to include intermediate nodes - apart from the last vertex's right neighbour,
            if (i < n) leftIndices.Add(vert.neighbourRight.vertexDown);
        }

        rightIndices.Insert(0, p3);
        rightIndices.Add(p1);

        for (int tri = 0 ; tri < rightIndices.Count - 1 ; tri += 1) AddTriangle(state, p0, rightIndices[tri], rightIndices[tri + 1]);


        // If n is odd, add middle triangles for p2

        if (n % 2 != 0) {
            TreeMeshNode l = ring0.GetPolygonNode(leftMax);
            TreeMeshNode m = l.neighbourRight; // intermediate node
            TreeMeshNode r = ring0.GetPolygonNode(rightMin);
            AddTriangle(state, p2, l.vertexDown, m.vertexDown);
            AddTriangle(state, p2, m.vertexDown, r.vertexDown);
        }
    }

    static int AddVertex(TreeMeshGeneratorState state, Vector3 pos) {
        state.vertices.Add(pos);
        return state.vertices.Count - 1;
    }

    static void AddTriangle(TreeMeshGeneratorState state, int v1, int v2, int v3) {
        state.triangles.Add(v1);
        state.triangles.Add(v2);
        state.triangles.Add(v3);
    }

    class TreeMeshNode {
        public Vector3 position { get; private set; }
        public TreeMeshNodeType type { get; private set; }
        // Neighbouring tree mesh nodes
        public TreeMeshNode neighbourUp, neighbourDown, neighbourLeft, neighbourRight;
        // The indices of the outer layer of vertices inside this node
        // For a simple node, these are all the same value
        // For a branch node, these vary
        public int vertexUp, vertexDown, vertexLeft, vertexRight;

        public TreeMeshNode(Vector3 pos) {
            position = pos;
            type = TreeMeshNodeType.Indeterminate;
        }

        public void ConfirmSimple(TreeMeshGeneratorState state) {
            Assert.IsTrue(type == TreeMeshNodeType.Indeterminate);

            type = TreeMeshNodeType.Simple;
            int vert = AddVertex(state, position);

            vertexUp = vert;
            vertexDown = vert;
            vertexLeft = vert;
            vertexRight = vert;
        }

        public void ConfirmBranch(TreeMeshGeneratorState state, Vector3 centre, Vector3 up, Vector3 down, Vector3 left, Vector3 right) {
            Assert.IsTrue(type == TreeMeshNodeType.Indeterminate);
            type = TreeMeshNodeType.Branch;

            position = centre;

            vertexUp = AddVertex(state, up);
            vertexDown = AddVertex(state, down);
            vertexLeft = AddVertex(state, left);
            vertexRight = AddVertex(state, right);
        }

        public void SetNeighbours(TreeMeshNode up, TreeMeshNode down, TreeMeshNode left, TreeMeshNode right) {
            neighbourUp = up;
            neighbourDown = down;
            neighbourLeft = left;
            neighbourRight = right;
        }
    }

    enum TreeMeshNodeType {
        Indeterminate, Simple, Branch
    }

    class TreeMeshNodeRing {
        List<TreeMeshNode> meshNodes;

        public TreeMeshNodeRing(TreeNode node, Vector3 normal, int resolution, TreeMeshNodeRing lowerRing, PlaneOrthoBasis previousBasis) {
            meshNodes = new();

            // Find the vertex positions
            PlaneOrthoBasis basis = MeshUtility.PlaneOrthoBasis(normal);
            if (previousBasis != null) basis = MeshUtility.PlaneOrthoBasis(normal, previousBasis.v1, previousBasis.v2);
            List<Vector3> vertexPositions = MeshUtility.ConstructRegularPolygon(basis, node.position, node.width, resolution);

            // Create the polygon vertex mesh nodes
            meshNodes = new();
            foreach (Vector3 pos in vertexPositions) meshNodes.Add(new(pos));

            // Link the polygon vertex nodes
            bool hasLowerRing = lowerRing != null;
            for (int i = 0 ; i < resolution ; i += 1) {
                // Up is null at this stage, but may be set by a later ring
                TreeMeshNode up = null;

                TreeMeshNode down = null;
                if (hasLowerRing) {
                    down = lowerRing.meshNodes[i];
                    down.SetNeighbours(meshNodes[i], down.neighbourDown, down.neighbourLeft, down.neighbourRight);
                }

                TreeMeshNode left = GetPolygonNode(i - 1);
                TreeMeshNode right = GetPolygonNode(i + 1);

                meshNodes[i].SetNeighbours(up, down, left, right);
            }

            // Add intermediate nodes between adjacent vertex nodes
            for (int i = 0 ; i < resolution ; i += 1) {
                TreeMeshNode left = GetPolygonNode(i);
                TreeMeshNode right = GetPolygonNode(i + 1);
                TreeMeshNode middle = new((left.position + right.position) / 2);
                // Look for the intermediate node directly below middle (if the lower ring exists)
                TreeMeshNode down = hasLowerRing ? left.neighbourDown.neighbourRight : null;

                left.SetNeighbours(left.neighbourUp, left.neighbourDown, left.neighbourLeft, middle);
                right.SetNeighbours(right.neighbourUp, right.neighbourDown, middle, right.neighbourRight);
                middle.SetNeighbours(null, down, left, right);
                if (down != null) down.SetNeighbours(middle, down.neighbourDown, down.neighbourLeft, down.neighbourRight);
            }
        }

        /// <summary>
        /// Get a mesh node at a given index, in a circular fashion (i.e. -1 ==> resolution - 1).
        /// Does not include intermediate nodes between vertices.
        /// </summary>
        public TreeMeshNode GetPolygonNode(int index) {
            int n = meshNodes.Count;
            int i = (n + index) % n;
            return meshNodes[i];
        }

        public int Resolution() {
            return meshNodes.Count;
        }
    }

}

public class GizmoData {
    public Vector3 start;
    public Vector3 end;
    public Color col;

    public GizmoData(Vector3 start, Vector3 end, Color col) {
        this.start = start;
        this.end = end;
        this.col = col;
    }
}
