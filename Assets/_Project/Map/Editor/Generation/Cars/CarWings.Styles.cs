using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Ready-made rear wing styles for the sporty hero cars, built from the CarWings parts: a swan-neck wing hung from
    /// its top surface, a body-colour wing on swept pylons, a tall gloss black GT wing on riser plates with big end
    /// plates, and a low boot-lid wing with an adjustable flap. Positions are in car space (wing = blade centre).
    /// </summary>
    internal static partial class CarWings
    {
        /// <summary>
        /// Swan-neck wing: two plate necks rise from the deck ahead of the blade, arch back over it and come down onto
        /// its top surface, so the underside stays clean.
        /// </summary>
        public static void SwanNeck(MeshBuilder mb, Vector3 wing, float halfSpan, float chord, float pitch, float neckX, float deckY,
            Material blade, Material neck)
        {
            Airfoil(mb, wing, halfSpan, chord, 0.11f, pitch, blade, 0.05f);
            float le = wing.z + chord * 0.5f, top = wing.y + chord * 0.07f;
            foreach (float s in new[] { -1f, 1f })
            {
                var line = new[]
                {
                    new Vector2(le + 0.07f, deckY - 0.01f), new Vector2(le + 0.065f, wing.y + 0.05f), new Vector2(le + 0.03f, top + 0.1f),
                    new Vector2(wing.z + chord * 0.1f, top + 0.11f), new Vector2(wing.z - chord * 0.05f, top + 0.06f),
                    new Vector2(wing.z - chord * 0.08f, top + 0.004f),
                };
                Strip(mb, line, 0.04f, s * neckX, 0.012f, neck);
                mb.Box(new Vector3(s * neckX, deckY + 0.004f, le + 0.07f), new Vector3(0.05f, 0.012f, 0.09f), neck);
                Bolt(mb, new Vector3(s * (neckX + 0.006f), top + 0.09f, le + 0.01f), Vector3.right * s, 0.006f);
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.55f, wing.y - 0.03f), new Vector2(wing.z + chord * 0.5f, wing.y + 0.06f),
                    new Vector2(wing.z - chord * 0.6f, wing.y + 0.09f), new Vector2(wing.z - chord * 0.6f, wing.y - 0.07f),
                }, s * (halfSpan + 0.003f), 0.005f, blade);
            }
        }

        /// <summary>Body-colour wing on two swept pylons standing on the engine cover, small end caps.</summary>
        public static void PylonWing(MeshBuilder mb, Vector3 wing, float halfSpan, float chord, float pitch, float pylonX, float deckY,
            Material paint)
        {
            Airfoil(mb, wing, halfSpan, chord, 0.13f, pitch, paint, 0.03f);
            foreach (float s in new[] { -1f, 1f })
            {
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.45f, deckY - 0.02f), new Vector2(wing.z + chord * 0.05f, wing.y - 0.01f),
                    new Vector2(wing.z - chord * 0.3f, wing.y - 0.01f), new Vector2(wing.z - chord * 0.05f, deckY - 0.02f),
                }, s * pylonX, 0.035f, paint);
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.5f, wing.y - 0.02f), new Vector2(wing.z + chord * 0.4f, wing.y + 0.04f),
                    new Vector2(wing.z - chord * 0.55f, wing.y + 0.06f), new Vector2(wing.z - chord * 0.55f, wing.y - 0.05f),
                }, s * (halfSpan + 0.006f), 0.012f, paint);
            }
        }

        /// <summary>Tall gloss black GT wing on two riser plates bolted to the deck, big rectangular end plates.</summary>
        public static void RiserWing(MeshBuilder mb, Vector3 wing, float halfSpan, float chord, float pitch, float riserX, float deckY,
            Material blade, Material riser)
        {
            Airfoil(mb, wing, halfSpan, chord, 0.12f, pitch, blade, 0.05f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * riserX;
                Plate(mb, new[]
                {
                    new Vector2(wing.z + 0.06f, deckY - 0.02f), new Vector2(wing.z + 0.04f, wing.y - 0.012f),
                    new Vector2(wing.z - 0.07f, wing.y - 0.012f), new Vector2(wing.z - 0.06f, deckY - 0.02f),
                }, x, 0.01f, riser);
                mb.Box(new Vector3(x, deckY + 0.005f, wing.z), new Vector3(0.05f, 0.012f, 0.15f), riser);
                foreach (float z in new[] { wing.z + 0.035f, wing.z - 0.035f })
                    Bolt(mb, new Vector3(x, deckY + 0.012f, z), Vector3.up, 0.006f);
                Hole(mb, x, wing.z, (deckY + wing.y) * 0.5f, 0.018f, 0.01f);
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.6f, wing.y - 0.05f), new Vector2(wing.z + chord * 0.6f, wing.y + 0.07f),
                    new Vector2(wing.z - chord * 0.65f, wing.y + 0.1f), new Vector2(wing.z - chord * 0.65f, wing.y - 0.09f),
                }, s * (halfSpan + 0.003f), 0.005f, blade);
            }
        }

        /// <summary>Low boot-lid wing in body colour on short stands, a black adjustable flap behind it.</summary>
        public static void BootWing(MeshBuilder mb, Vector3 wing, float halfSpan, float chord, float standX, float deckY, Material paint,
            Material flap)
        {
            Airfoil(mb, wing, halfSpan, chord, 0.14f, 3f, paint, 0.02f);
            Airfoil(mb, wing + new Vector3(0f, chord * 0.18f, -chord * 0.62f), halfSpan - 0.06f, chord * 0.38f, 0.1f, 24f, flap, 0.02f);
            foreach (float s in new[] { -1f, 1f })
            {
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.3f, deckY - 0.02f), new Vector2(wing.z + chord * 0.25f, wing.y),
                    new Vector2(wing.z - chord * 0.25f, wing.y), new Vector2(wing.z - chord * 0.3f, deckY - 0.02f),
                }, s * standX, 0.03f, paint);
                Plate(mb, new[]
                {
                    new Vector2(wing.z + chord * 0.5f, wing.y - 0.02f), new Vector2(wing.z + chord * 0.45f, wing.y + 0.03f),
                    new Vector2(wing.z - chord * 0.85f, wing.y + chord * 0.32f), new Vector2(wing.z - chord * 0.85f, wing.y - 0.02f),
                }, s * (halfSpan + 0.004f), 0.008f, paint);
            }
        }
    }
}
