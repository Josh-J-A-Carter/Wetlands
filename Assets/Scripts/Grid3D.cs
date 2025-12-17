using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


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

    int idMultX, idMultY, idMultZ;


    public Grid3D(Vector3 origin, int lengthX, int lengthY, int lengthZ, int gridDensity) {
        originWorldSpace = origin;

        this.lengthX = lengthX;
        this.lengthY = lengthY;
        this.lengthZ = lengthZ;

        this.gridDensity = gridDensity;

        idMultX = 1;
        idMultY = idMultX * lengthX * gridDensity * 2;
        idMultZ = idMultY * lengthY * gridDensity * 2;
    }

    public GridSet CastRayWorldSpace(Vector3 p, Vector3 dir, float radius, float length) {
        GridSet set = new(this);

        dir = dir.normalized;

        if (MeshUtility.Approximately(dir.magnitude, 0)) return set;

        PlaneOrthoBasis basis = MeshUtility.PlaneOrthoBasis(dir);

        for (float t = 0 ; t <= length ; t += 1.0f / gridDensity) {
            Vector3 centreWS = p + t * dir;

            // Iterate in a circle of radius r around centreWS, using the basis
            for (float r = 0 ; r <= radius ; r += 1.0f / gridDensity) {
                for (float theta = 0 ; theta <= 2 * Mathf.PI ; theta += 1.0f / (r * gridDensity)) {
                    Vector3 circleWS = centreWS + r * Mathf.Cos(theta) * basis.v1 + r * Mathf.Sin(theta) * basis.v2;
                    set.AddPointID(GridSpaceToIDSpace(WorldSpaceToGridSpace(circleWS)));
                }
            }
        }

        return set;
    }

    public void SetOccupied(GridSet set) {
        Assert.IsTrue(set.GetGrid() == this || set.GetGrid() == GridSet.Empty.GetGrid());

        foreach (int id in set.GetPointIDs()) {
            if (IsInBoundsIDSpace(id) == false) continue;

            gridOccupancy.Add(id);
        }
    }

    public bool IsOccupied(GridSet include, GridSet exclude) {
        Assert.IsTrue(include.GetGrid() == this || include.GetGrid() == GridSet.Empty.GetGrid());
        Assert.IsTrue(exclude.GetGrid() == this || exclude.GetGrid() == GridSet.Empty.GetGrid());

        // If there is an element 'x' in 'include' that is found in the grid, such that 'x' is not in 'exclude',
        // then return true

        foreach (int id in include.GetPointIDs()) {
            if (gridOccupancy.Contains(id) == false) continue;

            if (exclude.GetPointIDs().Contains(id)) continue;

            return true;
        }

        return false;
    }

    int GridSpaceToIDSpace(Vector3Int pos) {
        return idMultX * pos.x + idMultY * pos.y + idMultZ * pos.z;
    }

    Vector3Int IDSpaceToGridSpace(int id) {
        int x = id % idMultY / idMultX;
        int y = (id % idMultZ - x) / idMultY;
        int z = (id - x - y) / idMultZ;
        return new(x, y, z);
    }

    bool IsInBoundsIDSpace(int id) {
        return IsInBoundsGridSpace(IDSpaceToGridSpace(id));
    }

    bool IsInBoundsGridSpace(Vector3Int gs) {
        Vector3Int min = MinPointGridSpace();
        Vector3Int max = MaxPointGridSpace();
        return (gs.x >= min.x) && (gs.y >= min.y) && (gs.z >= min.z) && (gs.x <= max.x) && (gs.y <= max.y) && (gs.z <= max.z);
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
            Vector3Int pointGridSpace = IDSpaceToGridSpace(pointID);
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

public class GridSet {

    public static GridSet Empty { get; private set; } = new(null);
    Grid3D grid;
    List<int> pointIDs = new();

    /// <summary>
    /// A grid set is specific to a particular grid; it must not be used for another instance of Grid3D.
    /// </summary>
    /// <param name="grid"></param>
    public GridSet(Grid3D grid) {
        this.grid = grid;
    }

    public Grid3D GetGrid() {
        return grid;
    }
    
    public void AddPointID(int id) {
        pointIDs.Add(id);
    }

    public List<int> GetPointIDs() {
        return pointIDs;
    }
}
