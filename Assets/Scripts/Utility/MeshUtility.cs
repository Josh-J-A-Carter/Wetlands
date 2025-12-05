using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using Random = UnityEngine.Random;

public static class MeshUtility {

    const float EPSILON = 0.005f;

    /// <summary>
    /// Find the normal for the plane through a, b, and c.
    /// Assumes a, b, c are not contained in a line.
    /// </summary>
    public static Vector3 ComputePlaneNormal(Vector3 a, Vector3 b, Vector3 c) {
        Vector3 v1 = a - b;
        Vector3 v2 = a - c;
        return Vector3.Cross(v1, v2).normalized;
    }
    
    /// <summary>
    /// Finds a basis for the plane with this normal, which goes through the origin
    /// (so that it's a subspace, not an *affine* subspace)
    /// </summary>
    public static PlaneOrthoBasis PlaneOrthoBasis(Vector3 normal) {
        return PlaneOrthoBasis(normal, Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// Finds an orthonormal basis for a plane with this normal, and attempts to include 
    /// attemptBasisInclusion as the first vector of this basis.
    /// </summary>
    public static PlaneOrthoBasis PlaneOrthoBasis(Vector3 normal, Vector3 include1, Vector3 include2) {
        Vector3[] standardBasis = { include1, include2, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) };
        Vector3[] crossVectors = new Vector3[standardBasis.Length];

        for (int i = 0 ; i < standardBasis.Length ; i += 1) crossVectors[i] = Vector3.Cross(standardBasis[i], normal);

        Vector3 b1 = Vector3.zero;
        Vector3 b2 = Vector3.zero;
        
        foreach (Vector3 v in crossVectors) {
            if (Approximately(v.magnitude, 0)) continue;

            b1 = v.normalized;
            b2 = Vector3.Cross(v, normal).normalized;
            break;
        }

        return new(b1, b2);
    }

    // <summary> Find the angle that p makes in a circle in the plane with basis orthoBasis and origin at centre </summary>
    public static float PolygonVertexToAngle(Vector3 p, PlaneOrthoBasis basis, Vector3 centre) {
        // First, make sure p is in the span of basis!
        p = OrthoProjToPlane(p, basis.normal, centre);
        // Translate to origin, so that p = rcos(t) * v1 + rsin(t) * v2;
        Vector3 pPrime = p - centre;
        // ==> rcos(t) = p * v1, rsin(t) = p * v2
        float rcost = Vector3.Dot(pPrime, basis.v1);
        float rsint = Vector3.Dot(pPrime, basis.v2);

        return InvertCircle(rcost, rsint);
    }

    public static List<Vector3> ConstructRegularPolygon(PlaneOrthoBasis basis, Vector3 centre, float width, int resolution) {
        return ConstructRegularPolygonWithNormals(basis, centre, width, resolution).Item1;
    }

    public static Tuple<List<Vector3>, List<Vector3>> ConstructRegularPolygonWithNormals(PlaneOrthoBasis basis, Vector3 centre, float width, int resolution) {
        int n = resolution;

        List<Vector3> vertices = new();
        List<Vector3> normals = new();
        float deltaTheta = 2 * Mathf.PI / n;

        for (int i = 0 ; i < n ; i += 1) {
            // Consider the n-gon in the xy plane
            float theta = deltaTheta * i;
            Vector3 v = new(Mathf.Cos(theta), Mathf.Sin(theta), 0);
            // Linear transformation of (1, 0, 0) --> v1, (0, 1, 0) --> v2
            Vector3 vTransformed = v.x * basis.v1 + v.y * basis.v2;
            // Ensure correct length of vectors (it should be on the circle with radius = width)
            normals.Add(vTransformed.normalized);
            vTransformed = vTransformed.normalized * width;
            // Displacement
            Vector3 vTranslated = vTransformed + centre;
            vertices.Add(vTranslated);
        }

        return new(vertices, normals);
    }

    /// <summary>
    /// Project the vector p onto the line that has direction d and passes through x.
    /// </summary>
    public static Vector3 OrthoProjToLine(Vector3 p, Vector3 d, Vector3 x) {
        return x + (Vector3.Dot(p - x, d) / Vector3.Dot(d, d)) * d;
    }

    /// <summary>
    /// Project the vector p onto the plane that has (unit) normal n and passes through x
    /// </summary>
    public static Vector3 OrthoProjToPlane(Vector3 p, Vector3 n, Vector3 x) {
        return p - Vector3.Dot(p - x, n) * n;
    }

    /// <summary>
    /// Project p onto the line between x1 and x2, at pPrime = x1 + t (x2 - x1)
    /// so that (p - pPrime) and (x2 - x1) have the same angle as d and x2 - x1.
    /// Optional tMin and tMax to clamp the allowed t-values for pPrime; default to 0 and 1.
    /// </summary>
    public static Vector3 ObliqueProjToLine(Vector3 p, Vector3 d, Vector3 x1, Vector3 x2, float tMin = 0, float tMax = 1, bool clamp = true, bool verbose = false) {
        Vector3 a = x1 - p;
        Vector3 b = x2 - x1;
        Vector3 g = d.normalized;

        float aa = Vector3.Dot(a, a);
        float ab = Vector3.Dot(a, b);
        float bb = Vector3.Dot(b, b);
        float bg = Vector3.Dot(b, g);

        float A = bb*bb - bg*bg*bb;
        float B = 2*ab*bb - 2*bg*bg*ab;
        float C = ab*ab - bg*bg*aa;

        (float t1, float t2) = SolveQuadratic(A, B, C, float.NaN);

        // Easiest solution if the quadratic doesn't work out, is to just use an orthogonal projection
        if (float.IsNaN(t1)) return OrthoProjToLine(p, d, x1);

        Vector3 p1 = x1 + t1 * (x2 - x1);
        Vector3 p2 = x1 + t2 * (x2 - x1);

        float res1 = Mathf.Abs(1 - Vector3.Dot(p - p1, g) / (p - p1).magnitude);
        float res2 = Mathf.Abs(1 - Vector3.Dot(p - p2, g) / (p - p2).magnitude);

        float t = t1;
        if (res1 > res2) t = t2;

        if (verbose) {
            Debug.Log("Oblique line projection - verbose");
            Debug.Log("p1 " + p1 + ", t1 " + t1 + ", res1: " + res1);
            Debug.Log("p2 " + p2 + ", t2 " + t2 + ", res2: " + res2);
        }

        if (clamp && t < tMin) t = tMin;
        if (clamp && t > tMax) t = tMax;

        return x1 + t * (x2 - x1);
    }

    /// <summary>
    /// Find where the line, which goes through p1 and has direction p2 - p1, intersects
    /// the plane with normal n and passes through x.
    /// </summary>
    public static Vector3 IntersectLineWithPlane(Vector3 p1, Vector3 p2, Vector3 n, Vector3 x) {
        Vector3 d = (p2 - p1).normalized;
        n = n.normalized;

        float t = Vector3.Dot(n, x - p1) / Vector3.Dot(n, d);
        return p1 + t * d;
    }

    public static float InvertCircle(float rcosx, float rsinx) {
        float r = Mathf.Sqrt(rcosx * rcosx + rsinx * rsinx);
        if (rsinx >= 0) return Mathf.Acos(rcosx / r);
        return 2 * Mathf.PI - Mathf.Acos(rcosx / r);
    }

    public static bool Approximately(float a, float b) {
        return Mathf.Abs(a - b) < EPSILON;
    }

    /// <summary>
    /// Newton's root finding method
    /// </summary>
    public static float FindRoot(float x0, Func<float, float> f, Func<float, float> fPrime) {
        float xk = x0;
        int iter = 0;

        while (!Approximately(f(xk), 0.0f)) {
            xk -= f(xk) / fPrime(xk);

            iter += 1;
            Assert.IsTrue(iter < 100, "Root finding algorithm took too long: iter " + iter + ", xk " + xk + ", res " + f(xk));
        }

        return xk;
    }

    public static Tuple<float, float> SolveQuadratic(float a, float b, float c, float defaultVal) {
        float D = b*b - 4*a*c;

        if (D < 0 && Approximately(D, 0)) return new(-b/(2*a), -b/(2*a));
        
        if (D < 0) return new(defaultVal, defaultVal);

        float sqrt = Mathf.Sqrt(D);

        return new((-b-sqrt)/(2*a),(-b+sqrt)/(2*a));
    }

    public static Vector3 RandomVector() {
        const float PI = Mathf.PI;
        // Using surface patch for a sphere instead of picking a random vector and scaling
        // because that would not have a uniform distribution
        float theta = Random.Range(-PI / 2, PI / 2);
        float phi = Random.Range(0, 2 * PI);

        return Sphere(theta, phi);
    }

    public static Vector3 Sphere(float theta, float phi, float r = 1) {
        return new(r*Mathf.Cos(theta)*Mathf.Cos(phi), r*Mathf.Cos(theta)*Mathf.Sin(phi), r*Mathf.Sin(theta));
    }

    /// <summary>
    /// Obtain angles theta and phi for a given point on the sphere.
    /// </summary>
    /// <returns>First theta (-pi/2, pi/2), then phi (0, 2pi).</returns>
    public static Tuple<float, float> InvertSphere(Vector3 p) {
        // p = (rcos(theta)cos(phi), rcos(theta)sin(phi), rsin(theta))
        // ==> (rcos(theta))^2 = x^2 + y^2 ==> rcos(theta) = += sqrt(x^2 + y^2)
        // As theta is in (-pi/2, pi/2), cos(theta) >= 0 ==> rcos(theta) = sqrt(x^2 + y^2)
        // 
        // ==> cos(phi) = rcos(theta)*cos(phi)/rcos(theta) = x / sqrt(x^2 + y^2)
        // ==> sin(phi) = rcos(theta)*sin(phi)/rcos(theta) = y / sqrt(x^2 + y^2)
        // 
        // We have cos(phi), sin(phi), and rcos(theta), sin(theta), so can invert for phi and theta
        float x = p.x;
        float y = p.y;
        float z = p.z;

        float rcostheta = Mathf.Sqrt(x*x + y*y);
        float rsintheta = z;
        float cosphi = x / rcostheta;
        float sinphi = y / rcostheta;

        return new(InvertCircle(rcostheta, rsintheta), InvertCircle(cosphi, sinphi));
    }
}

public class PlaneOrthoBasis {
    public Vector3 v1 { get; private set; }
    public Vector3 v2 { get; private set; }

    public Vector3 normal { get; private set; }

    public PlaneOrthoBasis(Vector3 v1, Vector3 v2) {
        Assert.IsTrue(MeshUtility.Approximately(v1.magnitude, 1), "Basis vector magnitude != 1");
        Assert.IsTrue(MeshUtility.Approximately(v2.magnitude, 1), "Basis vector magnitude != 1");
        Assert.IsTrue(MeshUtility.Approximately(Vector3.Dot(v1, v2), 0), "Basis vectors are not orthogonal");

        this.v1 = v1.normalized;
        this.v2 = v2.normalized;
        normal = Vector3.Cross(v2, v1).normalized;
    }
}
