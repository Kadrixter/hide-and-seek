using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The crate model, built plank by plank (so it can fall apart into the same planks): sides of horizontal boards
    /// over a dark core, battens, an X brace on the back, a sprayed "ZŁOM / NIE RUSZAĆ" stencil split over the front
    /// boards. Breaking it throws the boards (physics, split where the saw went through), they settle and stay.
    /// </summary>
    public partial class SecurityRooms
    {
        private readonly Transform[] _crates = new Transform[Levels];
        private readonly List<Transform>[] _crateParts = { new(), new(), new() };
        private readonly List<GameObject>[] _debris = { new(), new(), new() };
        private Texture2D _stencilTex;

        private static readonly Color CrateWood = new Color(0.95f, 0.88f, 0.8f);
        private static readonly Color CrateBatten = new Color(0.72f, 0.62f, 0.52f);

        private void BuildCrate(Transform root, int lv)
        {
            var crate = new GameObject("SecurityCrate_L" + LevelName(lv)).transform;
            crate.SetParent(root, false);
            crate.SetPositionAndRotation(CrateClosed(lv), Quaternion.identity);
            var col = crate.gameObject.AddComponent<BoxCollider>();
            col.size = CrateSize;
            _crates[lv] = crate;
            var parts = _crateParts[lv];
            Vector3 h = CrateSize * 0.5f;
            var planks = Surf.T(SecurityTextures.Kind.Planks, 1.5f, 0.08f);

            Plank(crate, parts, "Core", Vector3.zero, CrateSize - Vector3.one * 0.06f, new Color(0.12f, 0.08f, 0.05f), default);
            for (int i = 0; i < 5; i++)
            {
                float y = -h.y + 0.15f + i * 0.3f;
                float shade = 0.9f + 0.1f * Mathf.Sin(i * 2.3f + lv);
                foreach (float sz in new[] { -1f, 1f })
                    Plank(crate, parts, "Board", new Vector3(0f, y, sz * (h.z - 0.015f)), new Vector3(CrateSize.x, 0.285f, 0.03f), CrateWood * shade, planks, i);
                foreach (float sx in new[] { -1f, 1f })
                    Plank(crate, parts, "SideBoard", new Vector3(sx * (h.x - 0.015f), y, 0f), new Vector3(0.03f, 0.285f, CrateSize.z - 0.06f), CrateWood * shade, planks, i);
            }
            for (int i = 0; i < 4; i++)
                Plank(crate, parts, "Lid", new Vector3(0f, h.y - 0.015f, -h.z + 0.13f + i * 0.26f), new Vector3(CrateSize.x - 0.04f, 0.03f, 0.25f), CrateWood * 0.92f, planks, i);
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * (h.z + 0.015f);
                Plank(crate, parts, "BattenTop", new Vector3(0f, h.y - 0.06f, z), new Vector3(CrateSize.x, 0.1f, 0.03f), CrateBatten, planks);
                Plank(crate, parts, "BattenBot", new Vector3(0f, -h.y + 0.06f, z), new Vector3(CrateSize.x, 0.1f, 0.03f), CrateBatten, planks);
                Plank(crate, parts, "BattenL", new Vector3(-h.x + 0.06f, 0f, z), new Vector3(0.1f, CrateSize.y - 0.2f, 0.03f), CrateBatten, planks);
                Plank(crate, parts, "BattenR", new Vector3(h.x - 0.06f, 0f, z), new Vector3(0.1f, CrateSize.y - 0.2f, 0.03f), CrateBatten, planks);
            }
            // X brace only on the back: the front carries the stencil.
            var diag = Plank(crate, parts, "BattenX", new Vector3(0f, 0f, h.z + 0.015f), new Vector3(0.09f, Mathf.Sqrt(CrateSize.x * CrateSize.x + CrateSize.y * CrateSize.y) - 0.3f, 0.028f), CrateBatten, planks);
            diag.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(CrateSize.x, CrateSize.y) * Mathf.Rad2Deg);

            // Stencil: one texture, cut into strips that stick to the front boards (they fly off with them).
            if (_stencilTex == null) _stencilTex = MakeStencilTexture();
            const float faceH = 1.3f;
            int board = 0;
            foreach (var t in parts)
            {
                if (t.name != "Board" || t.localPosition.z > 0f) continue;
                float y = t.localPosition.y;
                var strip = Sign(t, "Stencil", crate.TransformPoint(new Vector3(0f, y, -h.z - 0.002f)), new Vector2(CrateSize.x - 0.24f, 0.285f), Vector3.back, _stencilTex, 0.05f, true);
                strip.transform.localScale = new Vector3((CrateSize.x - 0.24f) / t.localScale.x, 0.285f / t.localScale.y, 1f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector("_MainTex_ST", new Vector4(1f, 0.285f / faceH, 0f, (y - 0.1425f + faceH * 0.5f) / faceH));
                strip.GetComponent<Renderer>().SetPropertyBlock(mpb);
                board++;
            }
        }

        private Transform Plank(Transform crate, List<Transform> parts, string n, Vector3 local, Vector3 size, Color c, Surf s, int variant = 0)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(crate, false);
            g.transform.localPosition = local;
            g.transform.localScale = size;
            if (s.Tex > 0) s.St = new Vector4(Mathf.Max(size.x, size.z) / 1.5f, Mathf.Max(0.1f, size.y / 1.5f), variant * 0.37f, variant * 0.2f);
            Paint(g.GetComponent<MeshRenderer>(), c, s, size);
            parts.Add(g.transform);
            return g.transform;
        }

        private static Texture2D MakeStencilTexture()
        {
            var tb = new TextureBaker(512, 460, Color.black);
            tb.Text("ZŁOM", 0.52f, 0.62f, 0.2f, Color.white, TextAnchor.MiddleCenter, true, 0.86f, 2f);
            tb.Text("NIE RUSZAĆ", 0.5f, 0.36f, 0.09f, Color.white, TextAnchor.MiddleCenter, true, 0.86f, -1f);
            tb.Rect(0.5f, 0.2f, 0.7f, 0.02f, Color.white);
            tb.Text("12 / 0347", 0.78f, 0.08f, 0.045f, Color.white, TextAnchor.MiddleCenter, true);
            return tb.FinishStencil("CrateStencil", new Color(0.08f, 0.06f, 0.05f), 3);
        }

        // ================================================================ breaking

        /// <summary>Crate falls apart: animated = boards thrown with physics (seen live), else laid out already settled.</summary>
        private void BreakCrate(int lv, bool animated)
        {
            if (_crateBroken[lv]) return;
            _crateBroken[lv] = true;
            var crate = _crates[lv];
            crate.GetComponent<Collider>().enabled = false;
            Vector3 centre = crate.position;
            float floor = FloorY(lv);
            var rnd = new System.Random(lv * 977 + 13);
            ClearKerf(lv);
            var thrown = new List<Rigidbody>();
            int k = 0;
            foreach (var part in _crateParts[lv])
            {
                part.gameObject.SetActive(false);
                if (part.name == "Core") continue;
                Vector3 size = part.lossyScale;
                var mr = part.GetComponent<MeshRenderer>();
                float len = Mathf.Max(size.x, size.y, size.z);
                // Long boards snap where the saw went through (roughly the middle, a bit random).
                int pieces = len > 0.7f ? 2 : 1;
                float cut = 0.35f + (float)rnd.NextDouble() * 0.3f;
                for (int pc = 0; pc < pieces; pc++)
                {
                    float f0 = pieces == 1 ? 0f : pc == 0 ? 0f : cut, f1 = pieces == 1 ? 1f : pc == 0 ? cut : 1f;
                    Vector3 axis = size.x >= size.y && size.x >= size.z ? part.right : size.y >= size.z ? part.up : part.forward;
                    Vector3 pieceSize = size;
                    if (axis == part.right) pieceSize.x *= f1 - f0; else if (axis == part.up) pieceSize.y *= f1 - f0; else pieceSize.z *= f1 - f0;
                    Vector3 pos = part.position + axis * (len * ((f0 + f1) * 0.5f - 0.5f));
                    var go = new GameObject(part.name + "_Piece");
                    go.transform.SetParent(crate.parent, false);
                    var mesh = go.AddComponent<MeshFilter>();
                    mesh.sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mr.sharedMaterial;
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    r.SetPropertyBlock(mpb);
                    SetupRenderer(r, true);
                    if (animated)
                    {
                        go.transform.SetPositionAndRotation(pos, part.rotation);
                        go.transform.localScale = pieceSize;
                        go.AddComponent<BoxCollider>();
                        var rb = go.AddComponent<Rigidbody>();
                        rb.mass = 1.5f + len;
                        rb.linearDamping = 0.2f;
                        rb.angularDamping = 0.3f;
                        Vector3 outDir = pos - centre; outDir.y = 0f;
                        outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : Vector3.back;
                        rb.linearVelocity = outDir * (1.6f + (float)rnd.NextDouble() * 2.2f) + Vector3.up * (1f + (float)rnd.NextDouble() * 2.5f) + Vector3.back * 0.8f;
                        rb.angularVelocity = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 14f;
                        thrown.Add(rb);
                    }
                    else
                    {
                        // Lying flat around the crate spot: thinnest side down, random yaw, scattered south / sideways.
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = 0.35f + (float)rnd.NextDouble() * 2.1f;
                        Vector3 spot = new Vector3(centre.x + Mathf.Cos(a) * d * 1.2f, floor, Mathf.Min(centre.z + 0.35f, centre.z + Mathf.Sin(a) * d));
                        float l = Mathf.Max(pieceSize.x, pieceSize.y, pieceSize.z), t = Mathf.Min(pieceSize.x, pieceSize.y, pieceSize.z);
                        float w = pieceSize.x + pieceSize.y + pieceSize.z - l - t;
                        go.transform.localScale = new Vector3(l, t, w);
                        go.transform.SetPositionAndRotation(spot + Vector3.up * (t * 0.5f + (k % 2) * t), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 6f - 3f));
                    }
                    _debris[lv].Add(go);
                    k++;
                }
            }
            // The bottom frame stays where the crate stood, broken.
            Vector3 bottom = new Vector3(centre.x, floor + 0.02f, centre.z);
            var stub1 = Box(crate.parent, "CrateStub", bottom + new Vector3(-0.35f, 0f, -0.3f), new Vector3(0.75f, 0.04f, 0.1f), CrateBatten, false, new Vector3(0f, 8f, 0f), Surf.T(SecurityTextures.Kind.Planks, 1.5f, 0.08f));
            var stub2 = Box(crate.parent, "CrateStub", bottom + new Vector3(0.45f, 0f, 0.25f), new Vector3(0.5f, 0.04f, 0.1f), CrateBatten, false, new Vector3(0f, -14f, 0f), Surf.T(SecurityTextures.Kind.Planks, 1.5f, 0.08f));
            var stub3 = Box(crate.parent, "CrateStub", bottom + new Vector3(0.62f, 0.12f, -0.42f), new Vector3(0.1f, 0.28f, 0.1f), CrateBatten, false, new Vector3(0f, 0f, 9f), Surf.T(SecurityTextures.Kind.Planks, 1.5f, 0.08f));
            _debris[lv].Add(stub1); _debris[lv].Add(stub2); _debris[lv].Add(stub3);
            if (animated)
            {
                SpawnChips(lv, centre + Vector3.up * 0.2f, Vector3.back, 70, 3.5f, true);
                if (_breakClip != null) GameAudio.PlayClipAt(_breakClip, centre, 1f, 34f);
                StartCoroutine(SettleDebris(thrown));
            }
            else SpawnSettledChips(lv, centre, 60);
            Debug.Log($"[Security] crate L{LevelName(lv)} broken (animated={animated}), {k} pieces");
        }

        /// <summary>After the boards stop, physics is removed: they stay as decoration and never trip anybody.</summary>
        private IEnumerator SettleDebris(List<Rigidbody> bodies)
        {
            yield return new WaitForSeconds(4.5f);
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                var c = rb.GetComponent<Collider>();
                Destroy(rb);
                if (c != null) Destroy(c);
            }
        }

        /// <summary>New round: the crate is whole again in front of the door.</summary>
        private void RestoreCrate(int lv)
        {
            _crateBroken[lv] = false;
            foreach (var g in _debris[lv]) if (g != null) Destroy(g);
            _debris[lv].Clear();
            ClearChips(lv);
            foreach (var part in _crateParts[lv]) part.gameObject.SetActive(true);
            _crates[lv].GetComponent<Collider>().enabled = true;
            _crateVis[lv] = SyncedState(lv) == CrateState.Open ? 1f : 0f;
            PlaceCrate(lv, 0f, 0f);
        }
    }
}
