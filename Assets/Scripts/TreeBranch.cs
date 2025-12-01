using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using UnityEngine;
using Random = UnityEngine.Random;

public class TreeBranch {

    const float START_WIDTH = 0.001f;
    const float START_LENGTH = 0.001f;
    
    int depth;

    int preSplitCount;

    TreeNode terminus;
    Vector3 terminusDirection;

    List<TreeNode> nodes;

    List<Tuple<int, TreeBranch>> sideBranches;
    List<Tuple<int, Vector3>> inactiveBuds;

    PlaneOrthoBasis phyllotaxyBasis;
    // Keep track of where we are in the leaf arrangement cycle,
    // i.e. what the previous leaf placement was
    int phyllotaxyState;

    public TreeBranch(Vector3 startPos, Vector3 direction, int depth, int preSplitCount = 0) {
        nodes = new();
        sideBranches = new();
        inactiveBuds = new();

        this.depth = depth;
        this.preSplitCount = preSplitCount;

        nodes.Add(new(startPos, START_WIDTH));

        terminusDirection = direction.normalized;
        terminus = new(startPos + terminusDirection * START_LENGTH, START_WIDTH);

        // Include some random variation for each branch to make it look more natural
        phyllotaxyBasis = MeshUtility.PlaneOrthoBasis(terminusDirection, MeshUtility.RandomVector(), Vector3.zero);
    }

    public int GetDepth() {
        return depth;
    }

    bool IsTerminalBranch(TreeParameters param) {
        int terminal = int.MaxValue;

        if (param.branchLengthByDepth.depth4 == 0) terminal = 4;
        if (param.branchLengthByDepth.depth3 == 0) terminal = 3;
        if (param.branchLengthByDepth.depth2 == 0) terminal = 2;
        if (param.branchLengthByDepth.depth1 == 0) terminal = 1;
        if (param.branchLengthByDepth.depth0 == 0) terminal = 0;

        return depth >= terminal - 1;
    }

    int MaxLength(TreeParameters param) {
        return depth switch {
            0 => param.branchLengthByDepth.depth0,
            1 => param.branchLengthByDepth.depth1,
            2 => param.branchLengthByDepth.depth2,
            3 => param.branchLengthByDepth.depth3,
            4 => param.branchLengthByDepth.depth4,
            _ => throw new Exception("Tree branch of depth > 4")
        };
    }

    bool HasReachedMaxGrowth(TreeParameters param) {
        return nodes.Count + preSplitCount > MaxLength(param);
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
        // Apply deltaPos to each node
        for (int i = 0 ; i < NodeCount() ; i += 1) GetNode(i).Item1.Translate(deltaPos);
        
        if (HasReachedMaxGrowth(param)) return;

        float growthVal = GrowthFactor(param) * light;
        float lengthGrowthFactor = growthVal * Tree.GROWTH_TICK_INCR;
        float widthGrowthFactor = param.widthToLenGrowthRatio * growthVal * Tree.GROWTH_TICK_INCR;

        // Enlarge each node
        for (int i = 0 ; i < NodeCount() ; i += 1) GetNode(i).Item1.Enlarge(widthGrowthFactor);

        // Recurse on child branches
        foreach ((int offshootIndex, TreeBranch branch) in sideBranches) {
            Vector3 offshootDir = (branch.nodes[0].position - nodes[offshootIndex].position).normalized;
            Vector3 deltaPosPrime = deltaPos + widthGrowthFactor * offshootDir;
            branch.Grow(light, deltaPosPrime, param);
        }

        // Move the terminal bud in its direction of growth
        terminus.Translate(terminusDirection * lengthGrowthFactor);

        float internodeLength = (terminus.position - nodes.Last().position).magnitude;

        if (internodeLength >= param.internodeLength) NewNode(param);

        // Decide to grow a new branch or not
        float chance = Random.Range(0.0f, 1.0f);
        if (chance < SideBranchChance(param) && inactiveBuds.Count > 0) {
            // Get the inactive bud with the lowest index
            int arrayIndex = 0;
            foreach ((int i, Vector3 d) in inactiveBuds) if (i < inactiveBuds[arrayIndex].Item1) { arrayIndex = i; }
            (int nodeIndex, Vector3 dir) = inactiveBuds[arrayIndex];

            // Need to find the starting position of the side branch;
            // This parent branch has a non-zero width!
            (TreeNode parentNode, Vector3 stemDir) = GetNode(nodeIndex);
            Vector3 dirInPlane = MeshUtility.OrthoProjToPlane(dir, stemDir, Vector3.zero).normalized;
            Vector3 startPos = parentNode.position + dirInPlane * parentNode.width;

            TreeBranch sideBranch = new(startPos, dir, depth + 1);
            sideBranches.Add(new(nodeIndex, sideBranch));

            inactiveBuds.RemoveAt(arrayIndex);
        }
    }

    private void NewNode(TreeParameters param) {
        nodes.Add(terminus);

        float chance = Random.Range(0.0f, 1.0f);
        if (chance < ApexSplitChance(param)) {
            // Account for any changes in basis as the branch has grown
            PlaneOrthoBasis localBasis = MeshUtility.PlaneOrthoBasis(terminusDirection, phyllotaxyBasis.v1, phyllotaxyBasis.v2);

            // Decide (semi-randomly) on the plane in which the split branches will live
            float alpha = Random.Range(0, 2 * Mathf.PI);
            Vector3 v1 = localBasis.v1 * Mathf.Cos(alpha) + localBasis.v2 * Mathf.Sin(alpha);
            Vector3 v2 = terminusDirection;
            // Assume branches will be pi radians separated (when projected onto localBasis)
            Vector3 dir1 = v1;
            Vector3 dir2 = -v1;
            // Use param for the angle between (old) terminusDirection and resulting branches
            float theta = param.apexSplitAngle;
            Vector3 b1 = v2 * Mathf.Cos(theta) + dir1 * Mathf.Sin(theta);
            Vector3 b2 = v2 * Mathf.Cos(theta) + dir2 * Mathf.Sin(theta);

            // b2 becomes a separate branch     **with the same depth**
            Vector3 startPos = terminus.position + b2 * terminus.width;
            TreeBranch sideBranch = new(startPos, NewTerminusDirection(param, b2), depth, preSplitCount: nodes.Count - 1);
            sideBranches.Add(new(nodes.Count - 1, sideBranch));

            // b1 becomes the new terminal direction for this branch
            terminus = new(terminus.position, 0.0f);
            terminusDirection = NewTerminusDirection(param, b1);

            // We do *not* add inactive buds at this node
        }

        else {
            terminus = new(terminus.position, 0.0f);
            terminusDirection = NewTerminusDirection(param, terminusDirection);

            //  Create new buds
            CreateBuds(param, nodes.Count - 1);

            //  Destroy old buds
            DestroyBuds(param, nodes.Count - 4); // magic number!!
        }
    }

    private Vector3 NewTerminusDirection(TreeParameters param, Vector3 dir) {
        (float theta, float phi) = MeshUtility.InvertSphere(dir);
        theta += Random.Range(-param.maxDirectionChangeAngle, param.maxDirectionChangeAngle);
        phi += Random.Range(-param.maxDirectionChangeAngle, param.maxDirectionChangeAngle);

        return MeshUtility.Sphere(theta, phi);
    }

    private float SideBranchChance(TreeParameters param) {
        float s = param.growthSpeed;
        float d = depth;
        float a = param.apicalDominance;
        if (IsTerminalBranch(param)) return 0; // No more side branches - otherwise performance will die
        return s * Mathf.Exp(-a * d) / (2 - a) / 1000;
    }

    private float ApexSplitChance(TreeParameters param) {
        float d = depth;
        float a = param.apicalDominance;
        if (d > 0) return 0; // Only allow splitting for depth 0 branches
        return Mathf.Exp(-5 * a) / 2;
    }

    private void CreateBuds(TreeParameters param, int nodeIndex) {
        if (IsTerminalBranch(param)) return;

        if (param.phyllotaxy == TreeParameters.Phyllotaxy.Whorled) {
            // May need to adjust the basis for this node, in case nodes change direction
            (_, Vector3 stemDir) = GetNode(nodeIndex);
            PlaneOrthoBasis localBasis = MeshUtility.PlaneOrthoBasis(stemDir, phyllotaxyBasis.v1, phyllotaxyBasis.v2);

            // dir1 and dir2, in opposite directions in the plane
            Vector3 dir1 = localBasis.v1;
            Vector3 dir2 = localBasis.v2;
            Vector3 dir3 = -localBasis.v1;
            Vector3 dir4 = -localBasis.v2;

            // dir1-4 are just the projections of the desired vectors onto localBasis
            // Instead, we want an angle theta with the stem direction
            float theta1 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);
            float theta2 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);
            float theta3 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);
            float theta4 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);
            Vector3 b1 = stemDir * Mathf.Cos(theta1) + dir1 * Mathf.Sin(theta1);
            Vector3 b2 = stemDir * Mathf.Cos(theta2) + dir2 * Mathf.Sin(theta2);
            Vector3 b3 = stemDir * Mathf.Cos(theta3) + dir3 * Mathf.Sin(theta3);
            Vector3 b4 = stemDir * Mathf.Cos(theta4) + dir4 * Mathf.Sin(theta4);

            inactiveBuds.Add(new(nodeIndex, b1.normalized));
            inactiveBuds.Add(new(nodeIndex, b2.normalized));
            inactiveBuds.Add(new(nodeIndex, b3.normalized));
            inactiveBuds.Add(new(nodeIndex, b4.normalized));
        }

        if (param.phyllotaxy == TreeParameters.Phyllotaxy.Opposite) {
            // May need to adjust the basis for this node, in case nodes change direction
            (_, Vector3 stemDir) = GetNode(nodeIndex);
            PlaneOrthoBasis localBasis = MeshUtility.PlaneOrthoBasis(stemDir, phyllotaxyBasis.v1, phyllotaxyBasis.v2);

            // dir1 and dir2, in opposite directions in the plane
            Vector3 dir1 = localBasis.v1;
            Vector3 dir2 = -localBasis.v1;

            // If planar, no op
            if (param.phyllotaxyAngleCycle == TreeParameters.PhyllotaxyCycle.Planar) {}
            // If decussate, rotate by 90 degrees
            if (param.phyllotaxyAngleCycle == TreeParameters.PhyllotaxyCycle.Decussate) {
                if (phyllotaxyState > 0) {
                    dir1 = localBasis.v2;
                    dir2 = -localBasis.v2;
                }
                
                phyllotaxyState += 1;
                phyllotaxyState %= 2;
            }

            // Place dir1 and dir2 so that they respect the branching angle with stemDir,
            // while remaining in the plane between localBasis.v1 & stemDir
            // 
            //    v1        -v1
            // <------- ^ ------->
            // \__      | s    __/
            //    \__   |   __/
            //  b1   \_ | _/   b2
            // 
            // where b1 and b2 make the angle branchingAngle with s = stemDir.
            float theta1 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);
            float theta2 = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);

            Vector3 b1 = stemDir * Mathf.Cos(theta1) + dir1 * Mathf.Sin(theta1);
            Vector3 b2 = stemDir * Mathf.Cos(theta2) + dir2 * Mathf.Sin(theta2);

            inactiveBuds.Add(new(nodeIndex, b1.normalized));
            inactiveBuds.Add(new(nodeIndex, b2.normalized));
        }

        if (param.phyllotaxy == TreeParameters.Phyllotaxy.Alternate) {
            // May need to adjust the basis for this node, in case nodes change direction
            (_, Vector3 stemDir) = GetNode(nodeIndex);
            PlaneOrthoBasis localBasis = MeshUtility.PlaneOrthoBasis(stemDir, phyllotaxyBasis.v1, phyllotaxyBasis.v2);

            // dir1 and dir2, in opposite directions in the plane
            Vector3 dir1 = localBasis.v1;

            // If planar, alternate branch sides
            if (param.phyllotaxyAngleCycle == TreeParameters.PhyllotaxyCycle.Planar) {
                if (phyllotaxyState > 0) dir1 = -localBasis.v1;

                phyllotaxyState += 1;
                phyllotaxyState %= 2;
            }
            
            // If spiral, rotate by 90 degrees each time
            if (param.phyllotaxyAngleCycle == TreeParameters.PhyllotaxyCycle.Spiral) {
                if (phyllotaxyState == 1) dir1 = localBasis.v2;
                if (phyllotaxyState == 2) dir1 = -localBasis.v1;
                if (phyllotaxyState == 3) dir1 = -localBasis.v2;
                
                phyllotaxyState += 1;
                phyllotaxyState %= 4;
            }

            float theta = Random.Range(param.minBranchingAngle, param.maxBranchingAngle);

            Vector3 b1 = stemDir * Mathf.Cos(theta) + dir1 * Mathf.Sin(theta);

            inactiveBuds.Add(new(nodeIndex, b1.normalized));
        }
    }

    private void DestroyBuds(TreeParameters param, int nodeIndex) {
        if (IsTerminalBranch(param)) return;

        if (nodeIndex < 0) return;

        for (int i = 0 ; i < inactiveBuds.Count ; i += 1) {
            if (inactiveBuds[i].Item1 != nodeIndex) continue;

            inactiveBuds.RemoveAt(i);
            i -= 1;
        }
    }

    private float GrowthFactor(TreeParameters param) {
        float s = param.growthSpeed;
        float d = depth;
        float a = param.apicalDominance;
        return s * Mathf.Exp(-a * d) * ((3*a + 1) / (a + 1));
    }

    public bool IsSplitBranch() {
        return preSplitCount > 0;
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