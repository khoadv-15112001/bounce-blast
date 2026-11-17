using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// The art team's BG model has a white plate (with two gold posts) on the fence, made for the old moves counter.
    /// The counter is in the top-left badge now, so this makes a copy of the BG mesh without the plate and the middle of
    /// the posts: the fence boards modelled underneath (one continuous face along the whole fence) show through, and the
    /// two horizontal rails with their brown edge stay untouched. The art's FBX is not modified.
    /// </summary>
    public static class BgPlateRemover
    {
        public const string OutPath = "Assets/GardenCannon/Art/" + Version + ".asset";   // file name = mesh name (Unity renames otherwise)
        const string BgPrefab = "Assets/3D/Prefab/Elements/BG.prefab";

        // Unity mesh space of BG.fbx (metres, x mirrored from the FBX). Plate + posts: x 2.77..4.86.
        // The rails' brown inner faces sit at z -0.643..-0.613 (top rail) and 0.478..0.508 (bottom rail): keep them, so
        // only what lies between z -0.605 and 0.47 and rises above the fence board face (y -0.183) is removed.
        const float XMin = 2.76f, XMax = 4.87f, ZMin = -0.605f, ZMax = 0.47f, YBoards = -0.181f;
        // Rail segment over the plate (x 2.9413..4.6838): the posts covered the joints with the next segments
        // (ending at 2.838 / starting at 4.7909); stretch its ends so the brown inner edge runs through.
        // Rails are bevelled with mitred ends: the bevel's top edge and the brown inner face end at different x.
        // Plate segment: top 2.9413 / 4.6838, bevel bottom + inner face 3.0013 / 4.6238.
        // Neighbours:    top 2.838  / 4.7909, bevel bottom + inner face 2.778  / 4.8509.
        static readonly float[] JointFrom = { 2.9413f, 3.0013f, 4.6838f, 4.6238f };
        static readonly float[] JointTo = { 2.838f, 2.778f, 4.7909f, 4.8509f };
        public const string Version = "BG_NoPlate_v4";

        [MenuItem("Garden Cannon/Rebuild BG without the moves plate", priority = 61)]
        public static Mesh Rebuild()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefab);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
            var src = mf != null ? mf.sharedMesh : null;
            if (src == null) { Debug.LogWarning("[GardenCannon] " + BgPrefab + " has no mesh."); return null; }

            var verts = src.vertices;
            // Close the rail joints left open by the removed posts (rail faces only: near the rails' z, above the floor).
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                bool rail = v.y > -0.56f && ((v.z > -0.775f && v.z < -0.60f) || (v.z > 0.47f && v.z < 0.64f));
                if (!rail) continue;
                for (int j = 0; j < JointFrom.Length; j++)
                    if (Mathf.Abs(v.x - JointFrom[j]) < 0.004f) { v.x = JointTo[j]; break; }
                verts[i] = v;
            }
            var mesh = new Mesh { name = Version, indexFormat = src.indexFormat };
            mesh.vertices = verts;
            mesh.normals = src.normals;
            if (src.tangents.Length == verts.Length) mesh.tangents = src.tangents;
            mesh.uv = src.uv;
            if (src.uv2.Length == verts.Length) mesh.uv2 = src.uv2;
            if (src.colors.Length == verts.Length) mesh.colors = src.colors;
            mesh.subMeshCount = src.subMeshCount;
            int removed = 0, total = 0;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                var tris = src.GetTriangles(sm);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                    Vector3 m = (a + b + c) / 3f;
                    total++;
                    float ymax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    float zmin = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), zmax = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    // Anything raised above the boards that reaches into the board area between the rails (the plate,
                    // the posts and the bevelled corners of the plate's frame); rail faces stay inside the rail bands.
                    bool plate = m.x > XMin && m.x < XMax && zmax > ZMin && zmin < ZMax && ymax > YBoards;
                    if (plate) { removed++; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                mesh.SetTriangles(keep, sm);
            }
            mesh.RecalculateBounds();
            if (removed == 0 || removed > total / 5)
            {
                Debug.LogWarning($"[GardenCannon] BG plate removal looks wrong ({removed}/{total} triangles), keeping the original BG.");
                Object.DestroyImmediate(mesh);
                return null;
            }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(OutPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, OutPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] {OutPath}: BG without the moves plate ({removed} of {total} triangles removed).");
            return mesh;
        }

        public static Mesh LoadOrBuild()
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(OutPath);
            return m != null && m.name == Version ? m : Rebuild();
        }
    }
}
