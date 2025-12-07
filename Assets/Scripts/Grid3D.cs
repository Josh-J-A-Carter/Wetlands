using System.Collections.Generic;
using UnityEngine;


public class Grid3D {
    
    Vector3 originWorldSpace = Vector3.zero;

    /// <summary>
    /// Length of worldspace coordinates, using worldspace notion of metres
    /// </summary>
    int lengthX = 5, lengthY = 5, lengthZ = 5;

    /// <summary>
    /// How many cells (gridspace) correspond to one metre (worldspace)
    /// </summary>
    int gridDensity = 4;

    /// <summary>
    /// Tracks grid occupancy. Each grid pos (x, y, z) is mapped to a unique value
    /// to be used as the key in the hash set.
    /// 
    /// If the point is in the set, it is occupied; otherwise it is unoccupied.
    /// </summary>
    HashSet<int> gridOccupancy = new();

    const int ID_MULT_X = 1, ID_MULT_Y = 1_000, ID_MULT_Z = 1_000_000;


    public Grid3D(Vector3 origin, int lengthX, int lengthY, int lengthZ, int gridDensity) {
        originWorldSpace = origin;

        this.lengthX = lengthX;
        this.lengthY = lengthY;
        this.lengthZ = lengthZ;

        this.gridDensity = gridDensity;

        // for (int x = MinPointGridSpace().x ; x <= MaxPointGridSpace().x ; x += 1) {
        //     for (int z = MinPointGridSpace().z ; z <= MaxPointGridSpace().z ; z += 1) {
        //         AddOccupied(new(x, MinPointGridSpace().y, z));
        //     }
        // }
        // AddRayWorldSpace(new(2, 2, 2), new(1, 1, 1), 0.2f, 3.0f);
    }

    public void AddRayWorldSpace(Vector3 p, Vector3 dir, float radius, float length) {
        dir = dir.normalized;

        if (MeshUtility.Approximately(dir.magnitude, 0)) return;

        PlaneOrthoBasis basis = MeshUtility.PlaneOrthoBasis(dir);

        for (float t = 0 ; t <= length ; t += 1.0f / gridDensity) {
            Vector3 centreWS = p + t * dir;

            // Iterate in a circle of radius r around centreWS, using the basis
            for (float r = 0 ; r <= radius ; r += 1.0f / gridDensity) {
                for (float theta = 0 ; theta <= 2 * Mathf.PI ; theta += 1.0f / (r * gridDensity)) {
                    Vector3 circleWS = centreWS + r * Mathf.Cos(theta) * basis.v1 + r * Mathf.Sin(theta) * basis.v2;
                    AddPointWorldSpace(circleWS);
                }
            }
        }
    }

    public void AddPointWorldSpace(Vector3 pos) {
        AddPointGridSpace(WorldSpaceToGridSpace(pos));
    }

    void AddPointGridSpace(Vector3Int pos) {
        // Only add if the point is within the bounds
        Vector3Int min = MinPointGridSpace();
        Vector3Int max = MaxPointGridSpace();
        
        if (pos.x < min.x || pos.y < min.y || pos.z < min.z) return;
        if (pos.x > max.x || pos.y > max.y || pos.z > max.z) return;

        gridOccupancy.Add(GridSpaceToSetID(pos));
    }

    int GridSpaceToSetID(Vector3Int pos) {
        return ID_MULT_X * pos.x + ID_MULT_Y * pos.y + ID_MULT_Z * pos.z;
    }

    Vector3Int SetIDToGridSpace(int id) {
        int x = id % ID_MULT_Y / ID_MULT_X;
        int y = (id % ID_MULT_Z - x) / ID_MULT_Y;
        int z = (id - x - y) / ID_MULT_Z;
        return new(x, y, z);
    }


    Vector3Int MinPointGridSpace() {
        return new(1, 1, 1);
    }

    Vector3Int MaxPointGridSpace() {
        return new(lengthX * gridDensity, lengthY * gridDensity, lengthZ * gridDensity);
    }

    public void DrawGizmos() {
        Vector3 min = GridSpaceToWorldSpace(MinPointGridSpace());
        Vector3 max = GridSpaceToWorldSpace(MaxPointGridSpace());


        // Bounding box outline
        GizmoManager.AddGizmo(min, new(min.x, min.y, max.z), Color.red);
        GizmoManager.AddGizmo(min, new(min.x, max.y, min.z), Color.red);
        GizmoManager.AddGizmo(min, new(max.x, min.y, min.z), Color.red);

        GizmoManager.AddGizmo(max, new(max.x, max.y, min.z), Color.red);
        GizmoManager.AddGizmo(max, new(max.x, min.y, max.z), Color.red);
        GizmoManager.AddGizmo(max, new(min.x, max.y, max.z), Color.red);

        GizmoManager.AddGizmo(new(max.x, min.y, min.z), new(max.x, max.y, min.z), Color.red);
        GizmoManager.AddGizmo(new(max.x, min.y, min.z), new(max.x, min.y, max.z), Color.red);

        GizmoManager.AddGizmo(new(min.x, min.y, max.z), new(min.x, max.y, max.z), Color.red);
        GizmoManager.AddGizmo(new(min.x, min.y, max.z), new(max.x, min.y, max.z), Color.red);

        GizmoManager.AddGizmo(new(min.x, max.y, min.z), new(max.x, max.y, min.z), Color.red);
        GizmoManager.AddGizmo(new(min.x, max.y, min.z), new(min.x, max.y, max.z), Color.red);


        // Draw all occupied cells
        foreach (int pointID in gridOccupancy) {
            Vector3Int pointGridSpace = SetIDToGridSpace(pointID);
            Vector3 pointWorldSpace = GridSpaceToWorldSpace(pointGridSpace);
            GizmoManager.AddGizmo(pointWorldSpace, Color.red);
        }
    }

    public Vector3Int WorldSpaceToGridSpace(Vector3 worldSpace) {
        Vector3 gs = (worldSpace - originWorldSpace) * gridDensity;
        // Can truncate since we only care about inside grid, where all coords are positive
        return new((int) gs.x, (int) gs.y, (int) gs.z);
    }

    public Vector3 GridSpaceToWorldSpace(Vector3Int gridSpace) {
        return (Vector3) gridSpace / gridDensity + originWorldSpace;
    }
}