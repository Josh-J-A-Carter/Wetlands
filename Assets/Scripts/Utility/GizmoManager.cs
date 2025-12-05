using System.Collections.Generic;
using UnityEngine;

public class GizmoManager : MonoBehaviour {

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

    static List<GizmoData> gizmos = new();

    static GizmoManager instance;

    void Start() {
        if (instance) {
            Destroy(this);
            return;
        }

        instance = this;
    }

    static public void AddGizmo(Vector3 p, Color col) {
        gizmos.Add(new(p, Vector3.zero, col));
    }

    static public void AddGizmo(Vector3 start, Vector3 end, Color col) {
        gizmos.Add(new(start, end, col));
    }

    private void OnDrawGizmos() {

        if (gizmos == null) return;

        foreach (GizmoData g in gizmos) {
            Gizmos.color = g.col;

            if (g.end != Vector3.zero) {
                Gizmos.DrawLine(g.start, g.end);
            }

            else Gizmos.DrawSphere(g.start, 0.005f);
        }
        gizmos.Clear();
    }

}
