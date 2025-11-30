using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

public static class TreeMeshGenerator {

    class TreeMeshGeneratorState {
        public Tree tree;
        public List<Vector3> vertices;
        public List<Vector3> normals;
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
            normals = new();
            triangles = new();
            gizmos = new();
        }
    }
    
    public static Tuple<List<Vector3>, List<int>, List<GizmoData>> Generate(Tree tree, Func<int, int> branchRingResolution) {
        TreeMeshGeneratorState state = new(tree, branchRingResolution);

        // Recursive branch generation algorithm
        GenerateBranch(state, tree.trunk, null);
        
        return new(state.vertices, state.triangles, state.gizmos);
    }

    static List<TreeMeshNodeRing> GenerateBranch(TreeMeshGeneratorState state, TreeBranch branch, PlaneOrthoBasis previousBasis) {

        // Create the tentative mesh structure
            // If this branch is already a side branch, we need to use its parent's basis to inform this one's
            // Otherwise, the connections between branches could become twisted
        List<TreeMeshNodeRing> branchStruct = BranchMeshStructure(state, branch, previousBasis);

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

                // Recurse on the branch
                List<TreeMeshNodeRing> sideBranchStruct = GenerateBranch(state, sideBranch, nBasis);

                // Project side branch base onto 
                CalculateSideBranchAttachment(state, n, branchStruct[nodeIndex], sideBranchStruct[0], sideBranchStruct[1], nBasis);
            }
        }

        // Create triangles

        // Moving frame of 4 mesh nodes
        TreeMeshNode n00 = branchStruct[0].GetNode(0);
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
        ConnectTerminalRing(state, branchStruct.Last());

        return branchStruct;
    }

    static void ConnectTerminalRing(TreeMeshGeneratorState state, TreeMeshNodeRing terminalRing) {
        TreeMeshNode n0 = terminalRing.GetNode(0);
        for (int i = 1 ; i < terminalRing.Resolution() - 1 ; i += 1) {
            TreeMeshNode n1 = terminalRing.GetNode(i);
            TreeMeshNode n2 = terminalRing.GetNode(i+1);
            int v0 = n0.index;
            int v1 = n1.index;
            int v2 = n2.index;
            AddTriangle(state, v1, v0, v2);
        }
    }


    static void CalculateSideBranchAttachment(TreeMeshGeneratorState state, TreeNode parentCentre, 
            TreeMeshNodeRing parentRing, TreeMeshNodeRing childRing, TreeMeshNodeRing nextChildRing, PlaneOrthoBasis attachmentBasis) {

        for (int i = 0 ; i < childRing.Resolution() ; i += 1) {
            // Determine which mesh nodes to insert this between
            TreeMeshNode baseNode = childRing.GetNode(i);
            float theta = MeshUtility.PolygonVertexToAngle(baseNode.position, attachmentBasis, parentCentre.position);
            // state.gizmos.Add(new(val * state.tree.transform.lossyScale.x, Vector3.zero, Color.red));
            // We have (2 * PI / n) * i =< theta =< (2 * PI / n) * (i + 1) for some integer i in [0, resolution - 1]
            // i =< theta * n / (2 * PI) =< i + 1
            // So i = floor(theta * n / (2 * PI))
            int vertexIndex = Mathf.FloorToInt(theta * parentRing.Resolution() / (2 * Mathf.PI));
            
            TreeMeshNode left = parentRing.GetNode(vertexIndex);
            TreeMeshNode right = parentRing.GetNode(vertexIndex + 1);

            // Determine if baseNode occurs above or below the plane in which parentRing's polygon occurs
            float belowOrAbove = Vector3.Dot(attachmentBasis.normal, baseNode.position - parentCentre.position);

            Vector3 v1 = right.position - left.position;
            Vector3 v2 = left.neighbourUp.position - left.position;

            if (belowOrAbove < 0) v2 = left.neighbourDown.position - left.position;

            Vector3 normal = Vector3.Cross(v1, v2).normalized;

            // Extend the line between baseNode and nextNode through to this plane defined by normal & left
            TreeMeshNode nextNode = nextChildRing.GetNode(i);
            Vector3 finalPos = MeshUtility.IntersectLineWithPlane(baseNode.position, nextNode.position, normal, left.position);
            // state.gizmos.Add(new(finalPos * state.tree.transform.lossyScale.x, Vector3.zero, Color.red));
            AdjustVertex(state, baseNode.index, finalPos);
        }
    }

    static List<TreeMeshNodeRing> BranchMeshStructure(TreeMeshGeneratorState state, TreeBranch branch, PlaneOrthoBasis basis) {
        List<TreeMeshNodeRing> rings = new();

        TreeMeshNodeRing prev = null;
        for (int i = 0 ; i < branch.NodeCount() ; i += 1) {
            (TreeNode n, Vector3 direction) = branch.GetNode(i);

            int res = state.branchRingResolution.Invoke(branch.GetDepth());
            TreeMeshNodeRing ring = new(state, n, direction, res, prev, basis);
            rings.Add(ring);
            prev = ring;
        }

        return rings;
    }

    static void ConnectMeshNodePanel(TreeMeshGeneratorState state, TreeMeshNode n00, TreeMeshNode n10,
                                                                    TreeMeshNode n01, TreeMeshNode n11) {
        AddTriangle(state, n00.index, n10.index, n11.index);
        AddTriangle(state, n00.index, n11.index, n01.index);
    }

    static int AddVertex(TreeMeshGeneratorState state, Vector3 pos, Vector3 normal) {
        state.vertices.Add(pos);
        state.normals.Add(normal);
        return state.vertices.Count - 1;
    }

    static void AdjustVertex(TreeMeshGeneratorState state, int index, Vector3 newPos) {
        state.vertices[index] = newPos;
    }

    static void AddTriangle(TreeMeshGeneratorState state, int v1, int v2, int v3) {
        state.triangles.Add(v1);
        state.triangles.Add(v2);
        state.triangles.Add(v3);
    }

    class TreeMeshNode {
        public Vector3 position { get; private set; }
        public Vector3 normal { get; private set; }
        // Neighbouring tree mesh nodes
        public TreeMeshNode neighbourUp, neighbourDown, neighbourLeft, neighbourRight;
        // The indices of the outer layer of vertices inside this node
        public int index;

        public TreeMeshNode(Vector3 position, Vector3 normal) {
            this.position = position;
            this.normal = normal;
        }

        public void SetNeighbours(TreeMeshNode up, TreeMeshNode down, TreeMeshNode left, TreeMeshNode right) {
            neighbourUp = up;
            neighbourDown = down;
            neighbourLeft = left;
            neighbourRight = right;
        }
    }

    class TreeMeshNodeRing {
        List<TreeMeshNode> meshNodes;

        public TreeMeshNodeRing(TreeMeshGeneratorState state, TreeNode node, Vector3 normal, int resolution,
                    TreeMeshNodeRing lowerRing, PlaneOrthoBasis previousBasis) {
            meshNodes = new();

            // Find the vertex positions & normals
            PlaneOrthoBasis basis = MeshUtility.PlaneOrthoBasis(normal);
            if (previousBasis != null) basis = MeshUtility.PlaneOrthoBasis(normal, previousBasis.v1, previousBasis.v2);
            (List<Vector3> positions, List<Vector3> normals) = MeshUtility.ConstructRegularPolygonWithNormals(basis, node.position, node.width, resolution);

            meshNodes = new();
            for (int i = 0 ; i < positions.Count ; i += 1) {
                int index = AddVertex(state, positions[i], normals[i]);
                meshNodes.Add(new(positions[i], normals[i]));
                meshNodes[i].index = index;
            }

            // Link the polygon vertex nodes
            bool hasLowerRing = lowerRing != null;
            for (int i = 0 ; i < resolution ; i += 1) {
                // Up is null at this stage, but will be set by a later ring (except for the last ring)
                TreeMeshNode up = null;

                TreeMeshNode down = null;
                if (hasLowerRing) {
                    down = lowerRing.meshNodes[i];
                    down.SetNeighbours(meshNodes[i], down.neighbourDown, down.neighbourLeft, down.neighbourRight);
                }

                TreeMeshNode left = GetNode(i - 1);
                TreeMeshNode right = GetNode(i + 1);

                meshNodes[i].SetNeighbours(up, down, left, right);
            }
        }

        /// <summary>
        /// Get a mesh node at a given index, in a circular fashion (i.e. -1 ==> resolution - 1).
        /// </summary>
        public TreeMeshNode GetNode(int index) {
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
