using UnityEngine;

namespace GardenCannon
{
    /// <summary>Small circle-vs-shape helpers. Normals point from the shape towards the circle.</summary>
    public static class Collision2D
    {
        public static bool CircleRect(Vector2 p, float r, Rect rect, out Vector2 normal, out float pen)
        {
            float cx = Mathf.Clamp(p.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(p.y, rect.yMin, rect.yMax);
            float dx = p.x - cx, dy = p.y - cy;
            float d2 = dx * dx + dy * dy;
            normal = Vector2.zero;
            pen = 0f;
            if (d2 >= r * r) return false;
            if (d2 < 1e-10f)
            {
                // Centre inside: push out along the closest side.
                float ol = p.x - rect.xMin, or = rect.xMax - p.x, ob = p.y - rect.yMin, ot = rect.yMax - p.y;
                float m = Mathf.Min(Mathf.Min(ol, or), Mathf.Min(ob, ot));
                if (m == ol) { normal = Vector2.left; pen = r + ol; }
                else if (m == or) { normal = Vector2.right; pen = r + or; }
                else if (m == ob) { normal = Vector2.down; pen = r + ob; }
                else { normal = Vector2.up; pen = r + ot; }
                return true;
            }
            float d = Mathf.Sqrt(d2);
            normal = new Vector2(dx / d, dy / d);
            pen = r - d;
            return true;
        }

        public static bool CircleCircle(Vector2 p, float r, Vector2 c, float R, out Vector2 normal, out float pen)
        {
            Vector2 delta = p - c;
            float d = delta.magnitude;
            normal = Vector2.up;
            pen = 0f;
            if (d >= r + R) return false;
            if (d > 1e-6f) normal = delta / d;
            pen = r + R - d;
            return true;
        }

        /// <summary>Convex polygon with counter-clockwise vertices.</summary>
        public static bool CirclePolygon(Vector2 p, float r, Vector2[] v, out Vector2 normal, out float pen)
        {
            normal = Vector2.zero;
            pen = 0f;
            int n = v.Length;
            bool inside = true;
            float bestEdgeDist = float.MaxValue;
            Vector2 bestEdgeNormal = Vector2.up;
            float bestD2 = float.MaxValue;
            Vector2 closest = p;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % n];
                Vector2 e = b - a;
                Vector2 outward = new Vector2(e.y, -e.x).normalized; // CCW -> right-hand normal points out
                float signed = Vector2.Dot(p - a, outward);
                if (signed > 0f) inside = false;
                float distIn = -signed;
                if (distIn < bestEdgeDist) { bestEdgeDist = distIn; bestEdgeNormal = outward; }

                float t = Mathf.Clamp01(Vector2.Dot(p - a, e) / Mathf.Max(e.sqrMagnitude, 1e-8f));
                Vector2 q = a + e * t;
                float d2 = (p - q).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; closest = q; }
            }
            if (inside)
            {
                normal = bestEdgeNormal;
                pen = r + bestEdgeDist;
                return true;
            }
            if (bestD2 >= r * r) return false;
            float d = Mathf.Sqrt(bestD2);
            normal = d > 1e-6f ? (p - closest) / d : bestEdgeNormal;
            pen = r - d;
            return true;
        }
    }
}
