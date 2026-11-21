using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Crate effects: wood chips and splinters (simple ballistic flight, they stay on the floor), the kerf growing along
    /// the cut, the crate shaking under the saw, and the synthesised sounds (scrape matched to the shoves, chainsaw biting
    /// into wood, the crate cracking apart with boards clattering down).
    /// </summary>
    public partial class SecurityRooms
    {
        private sealed class Chip
        {
            public Transform T;
            public Vector3 V, Spin;
            public bool Rest;
            public float Floor;
        }

        private const int MaxChipsPerRoom = 320;
        private readonly List<Chip>[] _chips = { new(), new(), new() };
        private readonly Transform[] _kerf = new Transform[Levels];
        private AudioClip _scrapeClip, _sawWoodClip, _breakClip;
        private float _nextChip;

        // ---------------------------------------------------------------- saw

        private static void SawLine(int lv, Vector3 crate, out Vector3 top, out Vector3 low)
        {
            float face = crate.z - CrateSize.z * 0.5f - 0.035f, floor = FloorY(lv);
            top = new Vector3(crate.x + 0.3f, floor + 1.25f, face);
            low = new Vector3(crate.x - 0.05f, floor + 0.5f, face);
        }

        private void BeginSawFx(int lv)
        {
            ClearKerf(lv);
            var k = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(k.GetComponent<Collider>());
            k.name = "Kerf";
            k.transform.SetParent(_crates[lv], true);
            Paint(k.GetComponent<MeshRenderer>(), new Color(0.05f, 0.03f, 0.02f), default, Vector3.one * 0.01f);
            k.SetActive(false);
            _kerf[lv] = k.transform;
            if (_sawWoodClip != null) StartCoroutine(PlayLater(_sawWoodClip, _crates[lv].position + Vector3.back * 0.6f, BlockyCharacterActions.CrateSawBiteStart - 0.05f));
        }

        private System.Collections.IEnumerator PlayLater(AudioClip clip, Vector3 pos, float delay)
        {
            yield return new WaitForSeconds(delay);
            GameAudio.PlayClipAt(clip, pos, 1f, 30f);
        }

        private void UpdateSawFx(int lv, float t)
        {
            if (_crateBroken[lv]) return;
            var crate = _crates[lv];
            Vector3 basePos = Vector3.Lerp(CrateClosed(lv), CrateOpen(lv), _crateVis[lv]);
            bool biting = t > BlockyCharacterActions.CrateSawBiteStart && t < BlockyCharacterActions.CrateSawBreakTime;
            float cut = BlockyCharacterActions.CrateCutProgress(t);
            SawLine(lv, basePos, out var top, out var low);
            Vector3 contact = Vector3.Lerp(top, low, cut);
            // The crate shudders while the chain bites.
            float sh = biting ? 0.004f + 0.006f * cut : 0f;
            crate.SetPositionAndRotation(basePos + new Vector3(Mathf.Sin(t * 87f), Mathf.Sin(t * 61f) * 0.4f, Mathf.Sin(t * 73f)) * sh,
                Quaternion.Euler(Mathf.Sin(t * 53f) * sh * 60f, Mathf.Sin(t * 41f) * sh * 80f, 0f));
            if (_kerf[lv] != null && biting)
            {
                _kerf[lv].gameObject.SetActive(true);
                Vector3 a = top, b = contact;
                _kerf[lv].position = (a + b) * 0.5f;
                _kerf[lv].rotation = Quaternion.LookRotation(Vector3.forward, (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector3.down);
                _kerf[lv].localScale = new Vector3(0.022f, Mathf.Max(0.02f, (b - a).magnitude), 0.02f);
            }
            if (biting && Time.time >= _nextChip)
            {
                _nextChip = Time.time + 0.02f;
                SpawnChips(lv, contact, Vector3.back + Vector3.up * 0.4f, 3, 2.6f, false);
            }
        }

        private void ClearKerf(int lv)
        {
            if (_kerf[lv] != null) Destroy(_kerf[lv].gameObject);
            _kerf[lv] = null;
        }

        // ---------------------------------------------------------------- chips

        private void SpawnChips(int lv, Vector3 at, Vector3 dir, int count, float speed, bool big)
        {
            var list = _chips[lv];
            for (int i = 0; i < count; i++)
            {
                Chip c;
                if (list.Count >= MaxChipsPerRoom) { c = list[0]; list.RemoveAt(0); }
                else
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(g.GetComponent<Collider>());
                    g.name = "Chip";
                    g.transform.SetParent(transform, false);
                    float shade = 0.75f + Random.value * 0.3f;
                    Paint(g.GetComponent<MeshRenderer>(), new Color(0.62f, 0.46f, 0.3f) * shade, default, Vector3.one * 0.01f);
                    c = new Chip { T = g.transform };
                }
                c.Rest = false;
                c.Floor = FloorY(lv) + 0.005f;
                c.T.position = at + Random.insideUnitSphere * 0.03f;
                c.T.rotation = Random.rotation;
                float len = big ? Random.Range(0.06f, 0.3f) : Random.Range(0.008f, 0.03f);
                c.T.localScale = big ? new Vector3(0.025f, 0.012f, len) : new Vector3(len, len * 0.3f, len * 0.6f);
                c.V = (dir.normalized + Random.insideUnitSphere * 0.7f).normalized * speed * Random.Range(0.4f, 1.1f);
                c.Spin = Random.insideUnitSphere * 900f;
                list.Add(c);
            }
        }

        /// <summary>Late join: sawdust and splinters already on the floor around the crate spot.</summary>
        private void SpawnSettledChips(int lv, Vector3 centre, int count)
        {
            var rnd = new System.Random(lv * 131 + 5);
            for (int i = 0; i < count; i++)
            {
                SpawnChips(lv, centre, Vector3.down, 1, 0f, i % 4 == 0);
                var c = _chips[lv][_chips[lv].Count - 1];
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = (float)rnd.NextDouble() * 1.6f;
                c.T.position = new Vector3(centre.x + Mathf.Cos(a) * d, c.Floor + c.T.localScale.y * 0.5f, Mathf.Min(centre.z + 0.4f, centre.z + Mathf.Sin(a) * d - 0.3f));
                c.T.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                c.Rest = true;
            }
        }

        private void UpdateChips()
        {
            float dt = Time.deltaTime;
            foreach (var list in _chips)
                foreach (var c in list)
                {
                    if (c.Rest || c.T == null) continue;
                    c.V += Physics.gravity * dt;
                    c.V *= 1f - 0.6f * dt;
                    Vector3 p = c.T.position + c.V * dt;
                    c.T.Rotate(c.Spin * dt, Space.Self);
                    float half = c.T.localScale.y * 0.5f;
                    if (p.y <= c.Floor + half)
                    {
                        p.y = c.Floor + half;
                        if (c.V.y < -1.5f && c.T.localScale.z > 0.05f) { c.V = new Vector3(c.V.x * 0.3f, -c.V.y * 0.25f, c.V.z * 0.3f); c.Spin *= 0.3f; }
                        else
                        {
                            c.Rest = true;
                            var e = c.T.eulerAngles;
                            c.T.rotation = Quaternion.Euler(0f, e.y, 0f);
                        }
                    }
                    c.T.position = p;
                }
        }

        private void ClearChips(int lv)
        {
            foreach (var c in _chips[lv]) if (c.T != null) Destroy(c.T.gameObject);
            _chips[lv].Clear();
        }

        // ---------------------------------------------------------------- sounds

        private void BuildCrateSounds()
        {
            _scrapeClip = MakeScrapeClip();
            _sawWoodClip = MakeSawWoodClip();
            _breakClip = MakeBreakClip();
        }

        /// <summary>Wood dragged over concrete, following the shoves: rumble + stick-slip judder + creaks, a knock at each stop.</summary>
        private static AudioClip MakeScrapeClip()
        {
            const int rate = 44100;
            float length = CratePushTime;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(77);
            float lp = 0f, lp2 = 0f, prevSlide = 0f, knock = 0f, knockF = 90f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float slide = BlockyCharacterActions.CrateSlide(t);
                if (i % 441 == 0)
                {
                    if (prevSlide > 0.25f && slide < 0.1f) { knock = 1f; knockF = 70f + (float)rnd.NextDouble() * 40f; }
                    prevSlide = slide;
                }
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.09f;
                lp2 += (lp - lp2) * 0.25f;
                float judder = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * (14f + 6f * Mathf.Sin(t * 3.1f)) * t));
                float creak = 0.22f * Mathf.Sin(2f * Mathf.PI * (210f + 70f * Mathf.Sin(t * 7f)) * t) * Mathf.Clamp01(Mathf.Sin(t * 31f) * 2f) * slide;
                knock *= 1f - 18f / rate;
                float knockS = knock * 0.7f * Mathf.Sin(2f * Mathf.PI * knockF * t) + knock * 0.25f * (float)(rnd.NextDouble() - 0.5);
                d[i] = Mathf.Clamp((lp2 * 6f * judder + creak) * Mathf.SmoothStep(0f, 1f, slide * 1.6f) + knockS, -1f, 1f);
            }
            var clip = AudioClip.Create("CrateScrape", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Chainsaw chain biting into wood: bogged-down engine buzz, harsh chip spray, stalls where it hits a batten.</summary>
        private static AudioClip MakeSawWoodClip()
        {
            const int rate = 44100;
            float length = BlockyCharacterActions.CrateSawBreakTime - BlockyCharacterActions.CrateSawBiteStart + 0.2f;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(91);
            float phase = 0f, hp = 0f, hpPrev = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float tt = t + BlockyCharacterActions.CrateSawBiteStart;
                float k = BlockyCharacterActions.CrateCutProgress(tt);
                float stall = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 4f));
                float f = 92f - 22f * stall + 6f * Mathf.Sin(t * 13f);
                phase += f / rate;
                float saw = (Mathf.Repeat(phase, 1f) * 2f - 1f) * 0.35f + Mathf.Sin(phase * 2f * Mathf.PI * 3f) * 0.15f;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                hp = 0.9f * (hp + w - hpPrev); hpPrev = w;
                bp += (hp - bp) * 0.35f;
                float chips = bp * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * f * 0.5f * t)));
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((length - t) / 0.12f);
                d[i] = Mathf.Clamp((saw * (0.8f + 0.3f * stall) + chips * 0.8f) * env, -1f, 1f);
            }
            var clip = AudioClip.Create("CrateSawWood", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Crate giving way: crack, heavy thump, then boards clattering on the concrete.</summary>
        private static AudioClip MakeBreakClip()
        {
            const int rate = 44100;
            const float length = 2.2f;
            int n = Mathf.CeilToInt(length * rate);
            var d = new float[n];
            var rnd = new System.Random(5);
            var knocks = new List<(float at, float f, float amp)>();
            for (int i = 0; i < 16; i++)
            {
                float at = 0.12f + Mathf.Pow((float)rnd.NextDouble(), 1.6f) * 1.6f;
                knocks.Add((at, 260f + (float)rnd.NextDouble() * 700f, 0.25f + 0.5f * (1f - at / 1.8f)));
            }
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.3f;
                float crack = Mathf.Exp(-t / 0.05f) * (w * 0.9f + Mathf.Sin(2f * Mathf.PI * 620f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 1130f * t) * 0.25f);
                float thump = Mathf.Exp(-t / 0.18f) * Mathf.Sin(2f * Mathf.PI * (70f - 20f * t) * t) * 0.9f;
                float splinter = Mathf.Exp(-t / 0.35f) * lp * 0.5f * (Mathf.Sin(t * 140f) > 0.3f ? 1f : 0.3f);
                float k = 0f;
                foreach (var (at, f, amp) in knocks)
                {
                    float x = t - at;
                    if (x < 0f || x > 0.12f) continue;
                    k += amp * Mathf.Exp(-x / 0.025f) * (Mathf.Sin(2f * Mathf.PI * f * x) * 0.7f + w * 0.3f);
                }
                d[i] = Mathf.Clamp(crack + thump + splinter + k, -1f, 1f);
            }
            var clip = AudioClip.Create("CrateBreak", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
