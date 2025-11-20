using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TreeBranch {
    
    public int depth { get; private set; }

    TreeNode terminus;
    Vector3 terminusDirection;

    List<TreeNode> nodes;

    public List<Tuple<int, TreeBranch>> sideBranches { get; private set; }


    public TreeBranch() {
        nodes = new();
        sideBranches = new();
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
        TreeNode nextNode = nextIndex == NodeCount() - 1 ? terminus : nodes[nextIndex];

        return new(nodes[index], nextNode.position - nodes[index].position);
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

}


public class TreeNode {
    public Vector3 position { get; private set; }
    public float width { get; private set; }

    public TreeNode(Vector3 position, float width) {
        this.position = position;
        this.width = width;
    }
}