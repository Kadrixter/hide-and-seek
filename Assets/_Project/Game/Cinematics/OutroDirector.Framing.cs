using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Where the winners stand and how the camera frames them, by their number (like the end of a CS:GO match):
    /// one winner stands alone and the camera circles him and moves in; two or three stand in a tight wedge; four to
    /// eight in an arc with the middle (the award holders) in front; nine and more in two staggered rows. Every shot
    /// is fitted to the figures for the current screen shape (the camera sits as close as the shot allows with every
    /// figure inside the free part of the screen: under the title, above the bars, between the award cards).
    /// </summary>
    public sealed partial class OutroDirector
    {
        /// <summary>Half width and half depth of a standing figure (blocky character, 16 x 8 px).</summary>
        const float FigHalfW = 0.47f, FigHalfD = 0.26f;

        /// <summary>Slot <paramref name="i"/> of <paramref name="n"/> in gate-local space (+Z into the garage).</summary>
        static Vector3 Slot(int i, int n)
        {
            if (n <= 1) return new Vector3(0f, 0f, RowZ + 0.4f);
            float c = (n - 1) * 0.5f, o = i - c;
            if (n <= 8)
            {
                // Arc: the middle in front, both ends further back; the step a little wider for few figures.
                float k = (n - 2) / 6f;
                float step = Mathf.Lerp(1.18f, 1.02f, k);
                float depth = n == 2 ? 0f : Mathf.Lerp(0.42f, 0.95f, (n - 3) / 5f);
                float u = o / c;
                return new Vector3(o * step, 0f, RowZ + depth * (1f - u * u));
            }
            // Two staggered rows (even indices in front), the back row in the gaps; no wider than the gate.
            float half = Mathf.Min(0.68f, 7.4f / (n - 1));
            float uu = o / c;
            return new Vector3(o * half, 0f, RowZ + (i % 2 == 0 ? 0.95f : 0f) + 0.35f * (1f - uu * uu));
        }

        /// <summary>Facing of the figure: towards the camera area, each a little different.</summary>
        float RowYaw(int i, int n)
        {
            Vector3 s = Slot(i, n);
            Vector3 focus = new Vector3(0.6f, 0f, RowZ + 8f);
            Vector3 d = focus - s;
            float jitter = n <= 1 ? 0f : ((i * 7) % 5 - 2) * 3f;
            Vector3 f = _gate.LocalDir(Quaternion.Euler(0f, jitter, 0f) * new Vector3(d.x, 0f, d.z).normalized);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>Walk-in start: the whole group comes in together from the side, keeping its shape.</summary>
        static Vector3 WalkFrom(int i, int n) => Slot(i, n) + new Vector3(-3.1f, 0f, 0.3f);

        /// <summary>When figure <paramref name="i"/> starts walking (the leading one, nearest the slots, first).</summary>
        static float WalkStart(int i, int n) => OutroTimeline.WalkIn + (n - 1 - i) * Mathf.Min(OutroTimeline.WalkStagger, 1.0f / Mathf.Max(1, n));

        // ================================================================ shots

        struct Shot
        {
            public float Yaw, Pitch, Fov, Bottom, Top, Zoom, Extend, Low, MaxH;
            public Vector2 U, V;
        }

        /// <summary>The shot of this moment, before smoothing: orientation, lens, what must be seen and where.</summary>
        Shot ShotAt(float t, int n)
        {
            // Overlay size in canvas units, computed like its CanvasScaler (1920 x 1080, match 0.5): the scaler only
            // runs in its Update, so on the first frame the rect is still in screen pixels and on a small window the
            // free box came out empty (the camera started 95 m away).
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            float scale = Mathf.Pow(2f, 0.5f * (Mathf.Log(sw / 1920f, 2f) + Mathf.Log(sh / 1080f, 2f)));
            float ch = sh / scale, cw = sw / scale;
            // Free part of the screen in NDC: under the title (its text ends ~320 canvas px from the top), above
            // the bottom bar and the nicks under the feet.
            float vTop = 1f - 2f * (322f / ch) - 0.03f;
            float vBottom = -1f + 2f * 0.105f + 2f * (52f / ch);
            var s = new Shot { U = new Vector2(-0.86f, 0.86f), V = new Vector2(vBottom, vTop), Top = 1.98f, Zoom = 1f, MaxH = 2.4f };
            if (t < OutroTimeline.Travel)
            {
                // Establishing: the stage lights up, the group walks in from the side.
                float k = CineKit.S(t, OutroTimeline.StageIn, OutroTimeline.Travel);
                s.Yaw = Mathf.Lerp(7f, 2f, k);
                s.Pitch = 6f;
                s.Fov = n <= 2 ? 42f : 46f;
                s.Extend = n <= 2 ? 1.6f : 1.2f;
                s.Zoom = Mathf.Lerp(1.14f, 1.04f, k);
                s.V = new Vector2(vBottom - 0.05f, vTop - 0.04f);
                return s;
            }
            if (t < OutroTimeline.Awards)
            {
                // The hero shot: a slow arc around the group with a push in, wider and higher for more figures.
                float k = Mathf.Clamp01((t - OutroTimeline.Travel) / (OutroTimeline.Awards - OutroTimeline.Travel));
                k = k * k * (3f - 2f * k);
                float a0, a1, pitch, fov, bottom;
                if (n <= 1) { a0 = -34f; a1 = 24f; pitch = 3f; fov = 34f; bottom = 1.1f; }
                else if (n == 2) { a0 = -22f; a1 = 16f; pitch = 4f; fov = 36f; bottom = 0.6f; }
                else if (n == 3) { a0 = -18f; a1 = 13f; pitch = 5f; fov = 38f; bottom = 0.32f; }
                else if (n <= 6) { a0 = -15f; a1 = 11f; pitch = 6f; fov = 40f; bottom = 0.12f; }
                else if (n <= 8) { a0 = -12f; a1 = 9f; pitch = 8f; fov = 42f; bottom = 0f; }
                else { a0 = -10f; a1 = 8f; pitch = 10f; fov = 44f; bottom = 0f; }
                s.Yaw = Mathf.Lerp(a0, a1, k);
                s.Pitch = pitch + (n <= 1 ? 3f * k : 0f);
                s.Fov = fov - (n <= 2 ? 2f * k : 0f);
                // The push in: from the whole figures to the waist (one winner) / the knees (two, three).
                s.Bottom = Mathf.Lerp(0f, bottom, n <= 2 ? CineKit.S(k, 0.02f, 0.75f) : CineKit.S(k, 0.1f, 0.95f));
                s.Zoom = bottom > 0.01f ? 1f : Mathf.Lerp(1.07f, 1f, k);
                if (n <= 1) s.U = new Vector2(-0.6f, 0.6f); // one figure: in the middle, not pushed to a side
                return s;
            }
            // Awards: the cards stand at both sides, the group fits in the middle between them.
            float ka = CineKit.S(t, OutroTimeline.Awards, OutroTimeline.End);
            s.Yaw = Mathf.Lerp(6f, -3f, ka);
            // A wide group between the cards needs a far camera: a wider lens keeps it closer, and low, under the
            // ceiling beams of the garage (with 15 figures a 52 deg lens put the camera into a beam).
            s.Pitch = n >= 9 ? 4f : 5f;
            s.Fov = n <= 3 ? 38f : Mathf.Min(62f, 42f + (n - 4) * 2f);
            s.MaxH = 2.1f;
            float inner = _cards.Count > 0 ? (CardInnerX() - 34f) / (cw * 0.5f) : 0.86f;
            s.U = new Vector2(-inner, inner);
            s.V = new Vector2(-0.8f, vTop - 0.02f);
            s.Zoom = Mathf.Lerp(1.04f, 1f, ka);
            s.Low = 0.3f; // a wide group: the spare height goes above it (the gate), not to the floor in front
            return s;
        }

        /// <summary>Distance of the inner edge of the award cards from the screen middle (canvas units).</summary>
        float CardInnerX()
        {
            float x = 1e5f;
            foreach (var (_, rt, home) in _cards) x = Mathf.Min(x, Mathf.Abs(home.x) - rt.sizeDelta.x * 0.5f);
            return x > 1e4f ? 900f : x;
        }

        readonly List<Vector3> _fitPts = new();

        /// <summary>World points the shot must show: a box around every figure's slot from <paramref name="bottom"/> up.</summary>
        void FitPoints(int n, float bottom, float top, float extend)
        {
            _fitPts.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector3 c = Slot(i, n);
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    _fitPts.Add(_gate.Local(c + new Vector3(sx * FigHalfW, bottom, sz * FigHalfD)));
                    _fitPts.Add(_gate.Local(c + new Vector3(sx * FigHalfW, top, sz * FigHalfD)));
                }
            }
            if (extend > 0f && n > 0)
            {
                // The way in (the figures enter the frame from the side).
                Vector3 a = WalkFrom(0, n);
                _fitPts.Add(_gate.Local(a + new Vector3(extend, 0f, 0f)));
                _fitPts.Add(_gate.Local(a + new Vector3(extend, top, 0f)));
            }
        }

        /// <summary>
        /// Camera position that shows every point inside the screen box <paramref name="u"/> x <paramref name="v"/>
        /// (NDC) with this rotation and lens, as close as possible (times <paramref name="zoom"/>), centred in the box.
        /// </summary>
        static Vector3 Fit(List<Vector3> pts, Quaternion rot, float fov, float aspect, Vector2 u, Vector2 v, float zoom, float low)
        {
            if (pts.Count == 0) return Vector3.zero;
            float tv = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), th = tv * aspect;
            var inv = Quaternion.Inverse(rot);
            // Camera space around the middle of the points (the dolly is measured from there).
            Vector3 centre = Vector3.zero;
            foreach (var p in pts) centre += p;
            centre /= pts.Count;
            var q = new Vector3[pts.Count];
            for (int i = 0; i < q.Length; i++) q[i] = inv * (pts[i] - centre);
            float lo = 0.2f, hi = 80f;
            for (int it = 0; it < 40; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Feasible(q, mid, th, tv, u, v, out _, out _, 0f)) hi = mid;
                else lo = mid;
            }
            float d = hi * Mathf.Max(1f, zoom);
            Feasible(q, d, th, tv, u, v, out float sx, out float sy, low);
            return centre + rot * new Vector3(sx, sy, -d);
        }

        /// <summary>At dolly <paramref name="d"/>, can a sideways / vertical shift fit every point? Gives the centred shift.</summary>
        static bool Feasible(Vector3[] q, float d, float th, float tv, Vector2 u, Vector2 v, out float sx, out float sy, float low)
        {
            float xl = float.MinValue, xh = float.MaxValue, yl = float.MinValue, yh = float.MaxValue;
            foreach (var p in q)
            {
                float z = p.z + d;
                if (z < 0.4f) { sx = sy = 0f; return false; }
                xl = Mathf.Max(xl, p.x - u.y * th * z);
                xh = Mathf.Min(xh, p.x - u.x * th * z);
                yl = Mathf.Max(yl, p.y - v.y * tv * z);
                yh = Mathf.Min(yh, p.y - v.x * tv * z);
            }
            sx = (xl + xh) * 0.5f;
            sy = Mathf.Lerp((yl + yh) * 0.5f, yh, low); // low > 0: the points sit lower in the box
            return xl <= xh && yl <= yh;
        }

        /// <summary>World camera pose for a shot (lower pitch if the camera would end up under the lamps).</summary>
        void ShotPose(in Shot s, int n, out Vector3 pos, out Quaternion rot)
        {
            FitPoints(n, s.Bottom, s.Top, s.Extend);
            float aspect = _cam.Cam.aspect;
            float pitch = s.Pitch;
            pos = Vector3.zero;
            rot = Quaternion.identity;
            // The ceiling beams hang down to 2.55 m: a far camera (a wide group) looks a little up rather than sit in one.
            for (int it = 0; it < 12; it++)
            {
                rot = _gate.transform.rotation * Quaternion.Euler(pitch, 180f + s.Yaw, 0f);
                pos = Fit(_fitPts, rot, s.Fov, aspect, s.U, s.V, s.Zoom, s.Low);
                float h = _gate.transform.InverseTransformPoint(pos).y;
                if (h < s.MaxH || pitch < -6f) break;
                pitch -= 1.5f;
            }
        }

        /// <summary>The camera of the outro: the shot of the moment, softly followed (no cuts).</summary>
        void UpdateCamera(float t)
        {
            if (_cam == null) return;
            int n = Mathf.Max(1, _puppets.Count);
            var shot = ShotAt(t, n);
            ShotPose(shot, n, out var wp, out var wr);
            if (!_camInit)
            {
                _camPos = wp; _camRot = wr; _camFov = shot.Fov; _camInit = true;
                Vector3 cl = _gate.transform.InverseTransformPoint(wp);
                Debug.Log($"[Cine] outro camera start t={t:0.00} n={n} at ({cl.x:0.00},{cl.y:0.00},{cl.z:0.00}) canvas={_cam.Overlay.rect.size}");
            }
            float s = 1f - Mathf.Exp(-Time.deltaTime * 2.6f);
            _camPos = Vector3.Lerp(_camPos, wp, s);
            _camRot = Quaternion.Slerp(_camRot, wr, s);
            _camFov = Mathf.Lerp(_camFov, shot.Fov, s);
            _cam.Set(_camPos, _camRot, _camFov);
            _cam.Bars = CineKit.S(t, OutroTimeline.StageIn, OutroTimeline.StageIn + 0.6f) * (1f - CineKit.S(t, OutroTimeline.Awards - 0.3f, OutroTimeline.Awards + 0.4f));
            _cam.Fade = 1f - CineKit.S(t, OutroTimeline.StageIn, OutroTimeline.StageIn + 0.65f);
        }

        Quaternion _camRot = Quaternion.identity;

        // ================================================================ tests

        /// <summary>Tests: where the figures are on the screen and whether something stands between them and the camera.</summary>
        public string FrameReport()
        {
            if (_cam == null) return "no camera";
            var cam = _cam.Cam;
            var sb = new System.Text.StringBuilder();
            Vector3 cl = _gate.transform.InverseTransformPoint(cam.transform.position);
            sb.Append($"t={_t:0.00} n={_puppets.Count} aspect={cam.aspect:0.00} fov={cam.fieldOfView:0.0} cam=({cl.x:0.00},{cl.y:0.00},{cl.z:0.00})");
            int blocked = 0, clipped = 0, walls = 0;
            for (int i = 0; i < _puppets.Count; i++)
            {
                var p = _puppets[i];
                if (p == null) continue;
                float x0 = 9f, x1 = -9f, y0 = 9f, y1 = -9f;
                foreach (float h in new[] { 0.05f, 0.9f, 1.75f })
                foreach (int sx in new[] { -1, 1 })
                {
                    Vector3 w = p.transform.position + Vector3.up * h + p.transform.right * (sx * 0.42f);
                    Vector3 vp = cam.WorldToViewportPoint(w);
                    x0 = Mathf.Min(x0, vp.x); x1 = Mathf.Max(x1, vp.x); y0 = Mathf.Min(y0, vp.y); y1 = Mathf.Max(y1, vp.y);
                }
                bool cut = x0 < 0f || x1 > 1f || y1 > 1f;
                if (cut) clipped++;
                Vector3 head = p.Body.Head.position + Vector3.up * 0.2f, chest = p.transform.position + Vector3.up * 1.1f;
                bool hit = Physics.Linecast(cam.transform.position, head, out var rh, ~0, QueryTriggerInteraction.Ignore) && !rh.transform.IsChildOf(p.transform);
                hit |= Physics.Linecast(cam.transform.position, chest, out var rc, ~0, QueryTriggerInteraction.Ignore) && !rc.transform.IsChildOf(p.transform);
                if (hit) blocked++;
                bool wall = Physics.CheckCapsule(p.transform.position + Vector3.up * 0.45f, p.transform.position + Vector3.up * 1.5f, 0.36f, ~0, QueryTriggerInteraction.Ignore);
                if (wall) walls++;
                sb.Append($"\n  [{i}] {p.Nick} x {x0:0.00}..{x1:0.00} y {y0:0.00}..{y1:0.00}{(cut ? " CUT" : "")}{(hit ? " BLOCKED by " + (rh.collider != null ? rh.collider.name : rc.collider != null ? rc.collider.name : "?") : "")}{(wall ? " IN-WALL" : "")}");
            }
            sb.Append($"\n  summary: clipped={clipped} blocked={blocked} inwall={walls}");
            return sb.ToString();
        }
    }
}
