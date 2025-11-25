using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class TreeBranch {
    
    public int depth { get; private set; }

    TreeNode terminus;
    Vector3 terminusDirection;

    List<TreeNode> nodes;

    public List<Tuple<int, TreeBranch>> sideBranches { get; private set; }
    public List<Tuple<int, Vector3>> inactiveBuds { get; private set; }

    public TreeBranch() {
        nodes = new();
        sideBranches = new();
        inactiveBuds = new();

        nodes.Add(new(Vector3.zero, 0.001f));
        terminus = new(new(0, 0.001f, 0), 0.001f);
        terminusDirection = Vector3.up;
    }

    public void TestAddNodes(List<TreeNode> nodes, TreeNode terminus, Vector3 dir) {
        this.terminus = terminus;
        terminusDirection = dir;
        this.nodes.AddRange(nodes);
    }

    public void TestAddBranch(TreeBranch b, int nodeIndex) {
        sideBranches.Add(new(nodeIndex, b));
    }

    // Get the index'th node in the branch, as well as its direction of growth.
    // Includes the terminal bud.
    public Tuple<TreeNode, Vector3> GetNode(int index) {
        Assert.IsTrue(index < NodeCount());

        if (index == NodeCount() - 1) return new(terminus, terminusDirection);

        int nextIndex = index + 1;
        Vector3 dir = nextIndex == NodeCount() - 1 ? terminusDirection : (nodes[nextIndex].position - nodes[index].position);
        
        return new(nodes[index], dir.normalized);
    }

    // Number of nodes in the branch, including the terminal bud.
    public int NodeCount() {
        return nodes.Count + 1;
    }


    // Get all the side branches for a given index;
    public List<TreeBranch> GetSideBranchesAt(int index) {
        Assert.IsTrue(index < NodeCount());

        List<TreeBranch> branches = new();
        foreach ((int i, TreeBranch b) in sideBranches) if (i == index) branches.Add(b);

        return branches;
    }

    public List<TreeBranch> GetAllSideBranches() {
        return new(sideBranches.Select(tuple => tuple.Item2));
    }

    public void Grow(float light, Vector3 deltaPos, TreeParameters param) {
        float growthVal = GrowthFactor(param) * light;
        float lengthGrowthFactor = growthVal * Tree.GROWTH_TICK_INCR;
        float widthGrowthFactor = param.widthToLenGrowthRatio * growthVal * Tree.GROWTH_TICK_INCR;

        // Apply deltaPos to each (non-terminal) node & enlarge as needed
        for (int i = 0 ; i < nodes.Count ; i += 1) {
            nodes[i].Translate(deltaPos);
            nodes[i].Enlarge(widthGrowthFactor);
        }

        // Recurse on child branches
        foreach ((int offshootIndex, TreeBranch branch) in sideBranches) {
            Vector3 offshootDir = (branch.nodes[0].position - nodes[offshootIndex].position).normalized;
            Vector3 deltaPosPrime = deltaPos + widthGrowthFactor * offshootDir;
            branch.Grow(light, deltaPosPrime, param);
        }

        // Update terminal bud
        terminus.Enlarge(widthGrowthFactor);
        terminus.Translate(deltaPos + terminusDirection * lengthGrowthFactor);

        float internodeLength = (terminus.position - nodes.Last().position).magnitude;

        if (internodeLength >= param.internodeLength) {
            TreeNode newTerminus = new(terminus.position, 0.0f);
            Vector3 newTerminusDirection = terminusDirection.normalized;

            nodes.Add(terminus);

            terminus = newTerminus;
            terminusDirection = newTerminusDirection;

            // 
            //  Create new buds
            // 

            // 
            //  Destroy old buds
            // 
        }
    }

    private float GrowthFactor(TreeParameters param) {
        float s = param.growthSpeed;
        float d = depth;
        float a = param.apicalDominance;
        return s * (Mathf.Exp(-a * d) / (2 - a)  + 1);
    }

}


public class TreeNode {
    public Vector3 position { get; private set; }
    public float width { get; private set; }

    public TreeNode(Vector3 position, float width) {
        this.position = position;
        this.width = width;
    }

    public void Translate(Vector3 deltaPos) {
        position += deltaPos;
    }

    public void Enlarge(float deltaWidth) {
        width += deltaWidth;
    }
}