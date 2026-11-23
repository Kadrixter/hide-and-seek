using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEditor;
using UnityEngine;
using M = HideAndSeek.Game.Editor.LobbySeatMeshes;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby seats (sofas, poufs): the final models. The lobby layout (MultiplayerBuilder) places placeholder "Sofa" /
    /// "Pouf" objects with their seats; <see cref="BuildSeatModels"/> replaces everything under them except the seats
    /// (LobbySeat) with the real model and its colliders. Sizes come from <see cref="SeatShape"/> (the seated poses are
    /// fitted to the same numbers): seats are low enough for the blocky rig's short legs to stand on the floor.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>Pouf model: 0 tufted chesterfield, 1 bean bag, 2 channel-tufted drum on legs, 3 wicker cube with a cushion.</summary>
        public static int PoufStyle = SeatShape.PoufStyle;

        /// <summary>Rebuilds every sofa / pouf under <paramref name="root"/> (called from BuildLobbyModels).</summary>
        private static void BuildSeatModels(Transform root)
        {
            if (!AssetDatabase.IsValidFolder(MeshDir))
            {
                System.IO.Directory.CreateDirectory(MeshDir);
                AssetDatabase.Refresh();
            }
            var furniture = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && (t.name == "Sofa" || t.name == "Pouf")) furniture.Add(t);
            foreach (var t in furniture)
            {
                Strip(t);
                if (t.name == "Sofa") BuildSofaModel(t);
                else BuildPoufModel(t, PoufStyle);
            }
        }

        /// <summary>Editor tools (SeatPoseStudio): rebuilds one sofa / pouf in the open scene (not saved).</summary>
        public static void RebuildSeatModel(Transform t, int poufStyle)
        {
            Strip(t);
            if (t.name == "Sofa") BuildSofaModel(t);
            else BuildPoufModel(t, poufStyle);
        }

        /// <summary>Removes the placeholder geometry (everything but the seats), keeps the transform where it is.</summary>
        private static void Strip(Transform t)
        {
            var kill = new List<GameObject>();
            foreach (Transform c in t)
                if (c.GetComponent<LobbySeat>() == null) kill.Add(c.gameObject);
            foreach (var g in kill) Object.DestroyImmediate(g);
            foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }

        // ================================================================ pieces

        /// <summary>A static mesh child (mesh saved as an asset under <paramref name="key"/>).</summary>
        private static GameObject Piece(Transform parent, string name, Mesh mesh, Material mat, string key)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, key);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            // Not static: the pose wheel preview copies these meshes (static batching would merge them).
            go.isStatic = false;
            return go;
        }

        /// <summary>Invisible box collider in local space.</summary>
        private static void BoxCol(Transform parent, Vector3 center, Vector3 size)
        {
            var bc = parent.gameObject.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
        }

        /// <summary>Convex cylinder collider (a pouf): radius, bottom / top height.</summary>
        private static void CylCol(Transform parent, string key, float radius, float y0, float y1)
        {
            var mesh = M.Param(16, 1, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(a) * radius, Mathf.Lerp(y0, y1, v), Mathf.Sin(a) * radius);
            });
            var mc = parent.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = SaveMesh(mesh, key);
            mc.convex = true;
        }

        // ================================================================ materials

        private static Texture2D VelvetTex() => SeatTex("seat_velvet", 128, 128, (x, y) =>
        {
            float u = x / 128f, v = y / 128f;
            float n = 0.9f + Hash01(x * 131 + y * 7919) * 0.07f + Mathf.PerlinNoise(u * 6f, v * 6f) * 0.08f - Mathf.PerlinNoise(u * 22f + 3f, v * 3f) * 0.05f;
            return new Color(n, n, n);
        });

        private static Texture2D CordTex() => SeatTex("seat_corduroy", 64, 64, (x, y) =>
        {
            float rib = (x % 4) < 2 ? 1f : (x % 4) == 2 ? 0.82f : 0.72f;
            float n = rib * (0.94f + Hash01(x * 37 + y * 101) * 0.06f) * (0.95f + Mathf.PerlinNoise(x / 20f, y / 9f) * 0.08f);
            return new Color(n, n, n);
        });

        private static Texture2D LinenTex() => SeatTex("seat_linen", 64, 64, (x, y) =>
        {
            float n = 0.9f + ((x + y) % 2 == 0 ? 0.04f : 0f) + Hash01(x * 53 + y * 977) * 0.06f - ((x % 8 == 0) ? 0.03f : 0f);
            return new Color(n, n * 0.99f, n * 0.97f);
        });

        /// <summary>Basket weave: flat strands alternating over / under in 8 px cells, darker gaps.</summary>
        private static Texture2D WickerTex() => SeatTex("seat_wicker", 64, 64, (x, y) =>
        {
            int cx = x / 8, cy = y / 8, lx = x % 8, ly = y % 8;
            bool horiz = (cx + cy) % 2 == 0;
            int across = horiz ? ly : lx, along = horiz ? lx : ly;
            float strand = across == 0 || across == 7 ? 0.45f : across == 1 || across == 6 ? 0.78f : 1f;
            float shade = 0.88f + 0.12f * Mathf.Sin(along / 8f * Mathf.PI); // strands curve over / under
            float grain = 0.93f + Hash01(x * 19 + y * 311) * 0.07f;
            float v = strand * shade * grain;
            return new Color(0.78f * v, 0.6f * v, 0.36f * v);
        });

        /// <summary>Like GenTex, but smooth (bilinear, mipmaps, anisotropic): fine fabric weaves would shimmer when point filtered.</summary>
        private static Texture2D SeatTex(string name, int w, int h, System.Func<int, int, Color> px)
        {
            var tex = GenTex(name, w, h, px);
            string path = AssetDatabase.GetAssetPath(tex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (imp.filterMode != FilterMode.Trilinear || !imp.mipmapEnabled))
            {
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return tex;
        }

        private static Material SeatMat(string name, Color c, Texture2D tex, float smooth, Vector2 tiling, float metallic = 0f) => Mat(name, c, tex, smooth, tiling, metallic);
    }
}
