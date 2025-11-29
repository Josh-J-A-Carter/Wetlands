using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using Unity.Burst.Intrinsics;

public static class TreeMeshGenerator {

    const int BRANCH_BASE_RESOLUTION = 4;

    class TreeMeshGeneratorState {
        public Tree tree;
        public List<Vector3> vertices;
        public List<int> triangles;

        public List<GizmoData> gizmos;

        /// <summary>
        /// Given a depth value, what should the corresponding resolution of the mesh be?
        /// i.e. 4 => square cylinder, 5 => pentagonal cylinder, etc.
        /// </summary>
        public Func<int, int> branchRingResolution;

        public TreeMeshGeneratorState(Tree tree, Func<int, int> branchRingResolution) {
            this.tree = tree;
            this.branchRingResolution = branchRingResolution;

            vertices = new();
            triangles = new();
            gizmos = new();
        }
    }
    
    public static Tuple<List<Vector3>, List<int>, List<GizmoData>> Generate(Tree tree, Func<int, int> branchRingResolution) {
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
        ConnectTerminalRing(state, branchMeshStructure.Last());
    }

    static void ConnectTerminalRing(TreeMeshGeneratorState state, TreeMeshNodeRing terminalRing) {
        // First, connect all the intermediate nodes in this ring together; we can connect them all to the same intermediate node
        int v0 = terminalRing.GetPolygonNode(0).neighbourRight.vertexUp;

        List<int> intermediateVertices = new();
        for (int i = 0 ; i < terminalRing.Resolution() ; i += 1) {
            intermediateVertices.Add(terminalRing.GetPolygonNode(i).neighbourRight.vertexUp);
        }

        for (int tri = 0 ; tri < terminalRing.Resolution() - 1 ; tri += 1) {
            int v1 = intermediateVertices[tri + 1];
            int v2 = intermediateVertices[tri];
            AddTriangle(state, v0, v1, v2);
        }

        // Now, connect each main polygon vertex to the intermediate ones directly to the left and right.
        for (int i = 0 ; i < terminalRing.Resolution() ; i += 1) {
            TreeMeshNode n = terminalRing.GetPolygonNode(i);
            v0 = n.vertexUp;
            int v1 = n.neighbourLeft.vertexUp;
            int v2 = n.neighbourRight.vertexUp;
            AddTriangle(state, v0, v1, v2);
        }
    }


    static Tuple<TreeMeshNode, PlaneOrthoBasis> CalculateSideBranchAttachment(TreeMeshGeneratorState state, TreeBranch sideBranch,
                        TreeMeshNodeRing attachmentRing, TreeNode attachmentNode, PlaneOrthoBasis attachmentBasis) {

        // Determine which mesh nodes to insert this between
        (TreeNode nPrime, Vector3 nPrimeNormal) = sideBranch.GetNode(0);
        float theta = MeshUtility.PolygonVertexToAngle(nPrime.position, attachmentBasis, attachmentNode.position);

        // We have (2 * PI / n) * i =< theta =< (2 * PI / n) * (i + 1) for some integer i in [0, resolution - 1]
        // i =< theta * n / (2 * PI) =< i + 1
        // So i = floor(theta * n / (2 * PI))
        int vertexIndex = Mathf.FloorToInt(theta * attachmentRing.Resolution() / (2 * Mathf.PI));

        TreeMeshNode left = attachmentRing.GetPolygonNode(vertexIndex);
        TreeMeshNode right = attachmentRing.GetPolygonNode(vertexIndex + 1);

        // Find the polygon (square) at the base of the side branch
        // and map this into the upper and lower planes on the main branch
        Vector3 lineDir = right.position - left.position;
        Vector3 upDir = left.neighbourUp.position - left.position;
        PlaneOrthoBasis nPrimeBasis = MeshUtility.PlaneOrthoBasis(nPrimeNormal, lineDir, upDir);
        List<Vector3> preProjection = MeshUtility.ConstructRegularPolygon(nPrimeBasis, nPrime.position,
                                                                        nPrime.width, BRANCH_BASE_RESOLUTION);
        
        // Assume that the base resolution is 4, for the purposes of the following code.
        Assert.IsTrue(BRANCH_BASE_RESOLUTION == 4);

        // centre, p0 & p2 are projected onto the line between left and right
        // Don't clamp, so we can see where pl and pr *would* end up on the line;
        // we want to detect if they go off either end of the line!
        Vector3 centre = MeshUtility.ObliqueProjToLine(nPrime.position, nPrimeNormal, left.position, right.position);
        Vector3 pl = MeshUtility.ObliqueProjToLine(preProjection[1], nPrimeNormal, left.position, right.position, clamp: false);
        Vector3 pr = MeshUtility.ObliqueProjToLine(preProjection[3], nPrimeNormal, left.position, right.position, clamp: false);

        // Determine where on this line segment the branch should go, i.e. is it in the middle, or on a corner?
        // If pl is before 'left' on the line, then 'left' is the node that should become a branch.
        // If pr is after 'right' on the line, then 'right' is the node that should become a branch
            // ==> This can be tested by solving   p = left + t * (right - left)   for t
            // ==> t = (p - left) . (right - left) / || right - left ||^2
            // If p is inside left and right on the line, then 0 < t < 1. Otherwise, p is before or after.
        Vector3 l = left.position;
        Vector3 r = right.position;
        float tl = Vector3.Dot(pl - l, r - l) / Vector3.Dot(r - l, r - l);
        float tr = Vector3.Dot(pr - l, r - l) / Vector3.Dot(r - l, r - l);

        if (tl < 0) {
            // p1 lies on the line between left and left.neighbourLeft, while p3 coincides with pr.
            Vector3 p1 = MeshUtility.ObliqueProjToLine(preProjection[1], nPrimeNormal, left.neighbourLeft.position, left.position);
            Vector3 p3 = pr;

            // Find the line along which to project p0
            Vector3 centreUp = MeshUtility.OrthoProjToLine(centre, right.neighbourUp.position - left.neighbourUp.position, left.neighbourUp.position);
            Vector3 p0 = MeshUtility.ObliqueProjToLine(preProjection[0], nPrimeNormal, centre, centreUp);

            // Find the line along which to project p2
            Vector3 centreDown = MeshUtility.OrthoProjToLine(centre, right.neighbourDown.position - left.neighbourDown.position, left.neighbourDown.position);
            Vector3 p2 = MeshUtility.ObliqueProjToLine(preProjection[2], nPrimeNormal, centreDown, centre);

            state.gizmos.Add(new(p0 * state.tree.transform.lossyScale.x, Vector3.zero, Color.red));
            state.gizmos.Add(new(p1 * state.tree.transform.lossyScale.x, Vector3.zero, Color.green));
            state.gizmos.Add(new(p2 * state.tree.transform.lossyScale.x, Vector3.zero, Color.blue));
            state.gizmos.Add(new(p3 * state.tree.transform.lossyScale.x, Vector3.zero, Color.yellow));


            // The 'left' node becomes the branch
            left.ConfirmBranch(state, centre, p0, p2, p1, p3);

            return new(left, nPrimeBasis);
        }

        else if (tr > 1) {
            // p3 lies on the line between right and right.neighbourRight, while p1 coincides with pl.
            Vector3 p1 = pl;
            Vector3 p3 = MeshUtility.ObliqueProjToLine(preProjection[3], nPrimeNormal, right.neighbourRight.position, right.position);

            // Find the line along which to project p0
            Vector3 centreUp = MeshUtility.OrthoProjToLine(centre, right.neighbourUp.position - left.neighbourUp.position, left.neighbourUp.position);
            Vector3 p0 = MeshUtility.ObliqueProjToLine(preProjection[0], nPrimeNormal, centre, centreUp);

            // Find the line along which to project p2
            Vector3 centreDown = MeshUtility.OrthoProjToLine(centre, right.neighbourDown.position - left.neighbourDown.position, left.neighbourDown.position);
            Vector3 p2 = MeshUtility.ObliqueProjToLine(preProjection[2], nPrimeNormal, centreDown, centre);

            state.gizmos.Add(new(p0 * state.tree.transform.lossyScale.x, Vector3.zero, Color.red));
            state.gizmos.Add(new(p1 * state.tree.transform.lossyScale.x, Vector3.zero, Color.green));
            state.gizmos.Add(new(p2 * state.tree.transform.lossyScale.x, Vector3.zero, Color.blue));
            state.gizmos.Add(new(p3 * state.tree.transform.lossyScale.x, Vector3.zero, Color.yellow));


            // The 'right' node becomes the branch
            right.ConfirmBranch(state, centre, p0, p2, p1, p3);
            
            return new(right, nPrimeBasis);
        }

        else {
            // pl and pr coincide with p1 and p3 due to all being on the same line segment
            Vector3 p1 = pl;
            Vector3 p3 = pr;

            // Find the line along which to project p0
            Vector3 centreUp = MeshUtility.OrthoProjToLine(centre, right.neighbourUp.position - left.neighbourUp.position, left.neighbourUp.position);
            Vector3 p0 = MeshUtility.ObliqueProjToLine(preProjection[0], nPrimeNormal, centre, centreUp);

            // Find the line along which to project p2
            Vector3 centreDown = MeshUtility.OrthoProjToLine(centre, right.neighbourDown.position - left.neighbourDown.position, left.neighbourDown.position);
            Vector3 p2 = MeshUtility.ObliqueProjToLine(preProjection[2], nPrimeNormal, centreDown, centre);

            state.gizmos.Add(new(p0 * state.tree.transform.lossyScale.x, Vector3.zero, Color.red));
            state.gizmos.Add(new(p1 * state.tree.transform.lossyScale.x, Vector3.zero, Color.green));
            state.gizmos.Add(new(p2 * state.tree.transform.lossyScale.x, Vector3.zero, Color.blue));
            state.gizmos.Add(new(p3 * state.tree.transform.lossyScale.x, Vector3.zero, Color.yellow));

            
            // Update the intermediate mesh node between left and right
            left.neighbourRight.ConfirmBranch(state, centre, p0, p2, p1, p3);

            return new(left.neighbourRight, nPrimeBasis);
        }
    }

    static List<TreeMeshNodeRing> BranchMeshStructure(TreeMeshGeneratorState state, TreeBranch branch, PlaneOrthoBasis previousBasis) {
        List<TreeMeshNodeRing> rings = new();

        TreeMeshNodeRing prev = null;
        for (int i = 0 ; i < branch.NodeCount() ; i += 1) {
            (TreeNode n, Vector3 direction) = branch.GetNode(i);

            int res = state.branchRingResolution.Invoke(branch.GetDepth());
            TreeMeshNodeRing ring = new(n, direction, res, prev, previousBasis);
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
