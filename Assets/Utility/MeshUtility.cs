using System.Collections.Generic;
using UnityEngine;

public static class MeshUtility {
    
    /// <summary>
    /// Finds a basis for the plane with this normal, which goes through the origin
    /// (so that it's a subspace, not an *affine* subspace)
    /// </summary>
    public static Vector3[] FindPlaneBasis(Vector3 normal) {
        Vector3[] standardBasis = { new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        Vector3[] crossVectors = new Vector3[3];
        for (int i = 0 ; i < 3 ; i += 1) crossVectors[i] = Vector3.Cross(standardBasis[i], normal);

        Vector3[] subspaceBasis = new Vector3[2];

        foreach (Vector3 v in crossVectors) {
            if (v == Vector3.zero) continue;

            subspaceBasis[0] = v;
            subspaceBasis[1] = Vector3.Cross(v, normal);
            break;
        }

        return subspaceBasis;
    }

}
