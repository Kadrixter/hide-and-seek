using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// 2026-11-27: a sofa spring popping out of the seat cushion (NetPlayer.SofaSpring). A steel coil shoots up out of
    /// the cushion with a twang and wobbles; from <see cref="NetPlayer.SpringPushStart"/> it goes down with the hands of
    /// the player pushing it back (<see cref="BlockyCharacterActions.SpringDepth"/>, three shoves), then vanishes into the
    /// cushion. Runs the same on every peer from one RPC.
    /// </summary>
    public sealed class SofaSpringFx : MonoBehaviour
    {
        public const float Height = 0.2f;
        private static Mesh _coil;
        private static Material _steel;

        private Vector3 _surface;
        private float _t, _pushStart;
        private int _shovesHeard;
        private Transform _coilT;

        public static void Play(Vector3 surface, float pushStart)
        {
            var go = new GameObject("SofaSpring");
            var fx = go.AddComponent<SofaSpringFx>();
            fx._surface = surface;
            fx._pushStart = pushStart;
            var c = new GameObject("Coil");
            c.transform.SetParent(go.transform, false);
            c.AddComponent<MeshFilter>().sharedMesh = Coil();
            c.AddComponent<MeshRenderer>().sharedMaterial = Steel();
            fx._coilT = c.transform;
            fx.Pose(0f, 0f);
            WireSounds.Pop(surface);
            WireSounds.Rattle(surface, 0.9f);
            WireSounds.Snap(surface);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float pop = Mathf.Clamp01(_t / 0.12f);
            // springy overshoot after the pop, dying out
            float wob = _t > 0.12f ? Mathf.Sin((_t - 0.12f) * 38f) * Mathf.Exp(-(_t - 0.12f) * 5f) : 0f;
            float h = pop * (1f + 0.25f * wob);
            float tp = _t - _pushStart;
            if (tp > 0f)
            {
                h = BlockyCharacterActions.SpringDepth(tp);
                int shove = Mathf.FloorToInt(Mathf.Clamp01((tp / BlockyCharacterActions.SpringPushDuration - 0.22f) / 0.58f) * 3f);
                if (tp / BlockyCharacterActions.SpringPushDuration > 0.22f && shove >= _shovesHeard && _shovesHeard < 3)
                {
                    _shovesHeard = shove + 1;
                    WireSounds.Creak(_surface, 0.8f);
                }
            }
            Pose(h, wob);
            if (tp > BlockyCharacterActions.SpringPushDuration + 0.2f)
            {
                WireSounds.Seat(_surface);
                Destroy(gameObject);
            }
            else if (_t > 20f) Destroy(gameObject); // safety
        }

        /// <summary>Coil base a little under the cushion; <paramref name="out01"/> = how far it stands out (1 = full height).</summary>
        private void Pose(float out01, float wob)
        {
            transform.position = _surface + Vector3.down * 0.05f;
            transform.rotation = Quaternion.Euler(8f * wob + 6f, 0f, -5f * wob);
            float stretch = Mathf.Max(0.05f, (0.05f + Height * out01) / (0.05f + Height));
            _coilT.localScale = new Vector3(1f, stretch, 1f);
            _coilT.gameObject.SetActive(out01 > 0.02f);
        }

        private static Material Steel()
        {
            if (_steel != null) return _steel;
            _steel = new Material(Shader.Find("Standard")) { name = "SofaSpringSteel", color = new Color(0.55f, 0.53f, 0.5f) };
            _steel.SetFloat("_Metallic", 0.8f);
            _steel.SetFloat("_Glossiness", 0.45f);
            return _steel;
        }

        /// <summary>Upholstery spring: 6 turns, 5.8 cm radius at the ends, 4 cm in the middle (hourglass), 4.5 mm wire, the top end bent over.</summary>
        private static Mesh Coil()
        {
            if (_coil != null) return _coil;
            var pts = new System.Collections.Generic.List<Vector3>();
            const int turns = 6, per = 14;
            float total = 0.05f + Height;
            for (int i = 0; i <= turns * per; i++)
            {
                float u = i / (float)(turns * per);
                float a = u * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(0.04f, 0.058f, Mathf.Abs(u - 0.5f) * 2f);
                pts.Add(new Vector3(Mathf.Cos(a) * r, u * total, Mathf.Sin(a) * r));
            }
            pts.Add(pts[pts.Count - 1] + new Vector3(-0.045f, 0.018f, 0.015f)); // the broken end sticking out
            var m = new ProcMesh();
            m.At(Vector3.zero).Tube(pts, 0.0045f, 6);
            _coil = m.ToMesh("SofaSpringCoil");
            return _coil;
        }
    }
}
