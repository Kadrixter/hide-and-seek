using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The found radio is the lobby boombox, a little worn: dusty body, a faded sticker on one end, a strip of grey duct
    /// tape over the back edge, a scuffed corner. Only added parts (and the body colour) - the model itself stays the same.
    /// </summary>
    public static class MapRadioLook
    {
        const float W = 0.56f, H = 0.27f, D = 0.15f;

        private static Material Mat(Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 size, float radius, Material m, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = VendingMeshes.RoundedBox(size, radius, 4);
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        static readonly Quaternion AntennaUp = Quaternion.Euler(-10f, 0f, 26f), AntennaFolded = Quaternion.Euler(0f, 0f, -88f);

        /// <summary>
        /// Telescopic antenna: up (0) or folded flat along the top back edge (1) - folded once the radio is carried, so it
        /// does not poke through the view of the carrier or into walls.
        /// </summary>
        public static void FoldAntenna(Boombox b, float k)
        {
            var ant = b.Visual != null ? b.Visual.Find("Antenna") : null;
            if (ant != null) ant.localRotation = Quaternion.Slerp(AntennaUp, AntennaFolded, Mathf.SmoothStep(0f, 1f, k));
        }

        public static void Wear(Boombox b)
        {
            var v = b.Visual;
            if (v == null) return;
            foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
                if (r.name == "Body") r.material.color = new Color(0.1f, 0.095f, 0.088f); // dust on the black plastic

            // Sticker on the +X end: faded yellow with a red disc and a dark band, slightly crooked.
            var end = Quaternion.Euler(0f, 90f, 7f);
            Part(v, "Sticker", new Vector3(W * 0.5f + 0.0015f, 0.15f, 0.005f), new Vector3(0.085f, 0.065f, 0.0016f), 0.006f, Mat(new Color(0.86f, 0.74f, 0.3f), 0.35f), end);
            Part(v, "StickerDot", new Vector3(W * 0.5f + 0.0028f, 0.158f, 0.017f), new Vector3(0.03f, 0.03f, 0.0012f), 0.0145f, Mat(new Color(0.72f, 0.16f, 0.12f), 0.35f), end);
            Part(v, "StickerBand", new Vector3(W * 0.5f + 0.0028f, 0.133f, -0.006f), new Vector3(0.07f, 0.011f, 0.0012f), 0.003f, Mat(new Color(0.12f, 0.11f, 0.1f), 0.3f), end);

            // Duct tape over the back top edge (a repair): on the top and down the back.
            var tape = Mat(new Color(0.55f, 0.56f, 0.57f), 0.45f);
            Part(v, "TapeTop", new Vector3(-0.115f, H + 0.0012f, -D * 0.5f + 0.035f), new Vector3(0.05f, 0.0024f, 0.07f), 0.001f, tape, Quaternion.Euler(0f, 4f, 0f));
            Part(v, "TapeBack", new Vector3(-0.117f, H - 0.04f, -D * 0.5f - 0.0012f), new Vector3(0.05f, 0.08f, 0.0024f), 0.001f, tape, Quaternion.Euler(0f, 0f, 4f));

            // Scuffed corner: worn grey plastic on the front bottom corner of the -X end.
            Part(v, "Scuff", new Vector3(-W * 0.5f + 0.012f, 0.012f, D * 0.5f - 0.012f), new Vector3(0.03f, 0.024f, 0.03f), 0.01f, Mat(new Color(0.2f, 0.2f, 0.2f), 0.2f), Quaternion.identity);
        }
    }
}
