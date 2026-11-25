using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -testpool checks on every peer, logged per shot as PASS / FAIL lines:
    /// "body" - no player at the table sinks into it (every vertex of every body part, in table space, against the rails,
    /// the cushions, the bed and the apron): torso, head and legs never (15 mm), the thick blocky arms at most 45 mm
    /// (a forearm resting on the rail's edge); "camera" - the own board camera never shows
    /// the own body and no other player's body comes within a metre of it (drawn, not as a shadow); "inning" (host) - teams alternate, a team's two
    /// players take its innings in turn.
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        readonly Dictionary<Mesh, Vector3[]> _verts = new();
        Mesh _baked;
        Camera _checkCam;
        int _bodyShots;

        IEnumerator PoolChecks(PoolTable pool)
        {
            int shot = pool.State.ShotNo, place = -1;
            float bodyMax = 0f, bend = 0f, coreMax = 0f;
            string bodyWho = "-", coreWho = "-";
            int ownSeen = 0, occl = 0, frames = 0;
            string occlWho = "";
            while (true)
            {
                yield return new WaitForSeconds(0.12f);
                var s = pool.State;
                if (s.ShotNo != shot || s.Phase != PoolState.Playing && frames > 0)
                {
                    if (frames > 0)
                    {
                        Log($"body: shot {shot + 1} max {bodyMax * 1000f:0} mm ({bodyWho}, bend {bend:0.00}), torso / head / legs max {coreMax * 1000f:0} mm ({coreWho}) => {(bodyMax < 0.045f && coreMax < 0.015f ? "OK" : "FAIL")}");
                        if (place >= 0) Log($"camera: shot {shot + 1} own body visible {ownSeen}x, players in front {occl}x {occlWho} => {(ownSeen == 0 && occl == 0 ? "OK" : "FAIL")}");
                    }
                    shot = s.ShotNo;
                    bodyMax = bend = coreMax = 0f;
                    coreWho = "-";
                    bodyWho = "-";
                    ownSeen = occl = frames = 0;
                    occlWho = "";
                }
                if (s.Phase != PoolState.Playing) continue;
                frames++;
                place = pool.LocalSeat;
                for (int p = 0; p < PoolState.Places; p++)
                {
                    var pl = NetPlayer.Find(s.Place(p));
                    if (pl == null || pl.Character == null) continue;
                    float pen = BodyPenetration(pl, pool, out string part, out Vector3 pt, out float core, out string corePart);
                    if (core > coreMax) { coreMax = core; coreWho = $"{pl.Nick} {corePart}"; }
                    if (pen > bodyMax)
                    {
                        bodyMax = pen;
                        Vector3 root = pool.transform.InverseTransformPoint(pl.transform.position);
                        bodyWho = $"{pl.Nick} place {p} {part} at {pt.ToString("F2")}, root {root.ToString("F2")}, {pool.TestPoseInfo(p)}";
                        if (pen > 0.04f && _bodyShots < 8)
                        {
                            _bodyShots++;
                            Vector3 o = new Vector3(pt.x, 0f, pt.z).normalized;
                            Render($"{Prefix()}body_{_bodyShots}_{pl.Nick}", pool.transform.TransformPoint(pt + o * 1.3f + Vector3.up * 0.35f + Vector3.Cross(Vector3.up, o) * 0.5f), pool.transform.TransformPoint(pt), 45f);
                        }
                        bend = pool.TestBend(p);
                    }
                }
                if (place >= 0 && MiniGameCamera.Weight > 0.95f && Time.frameCount - _renderFrame > 2) CameraCheck(pool, place, ref ownSeen, ref occl, ref occlWho);
            }
        }

        /// <summary>Deepest point of a player's body inside the table (m) and the part it belongs to.</summary>
        float BodyPenetration(NetPlayer pl, PoolTable pool, out string part, out Vector3 at, out float core, out string corePart)
        {
            part = corePart = "-";
            at = default;
            float max = 0f;
            core = 0f;
            Matrix4x4 toTable = pool.transform.worldToLocalMatrix;
            foreach (var r in pl.Character.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Vector3[] v = null;
                Matrix4x4 m = toTable * r.transform.localToWorldMatrix;
                if (r is SkinnedMeshRenderer sk)
                {
                    _baked ??= new Mesh();
                    sk.BakeMesh(_baked);
                    v = _baked.vertices;
                    m = toTable * Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (!_verts.TryGetValue(mf.sharedMesh, out v)) _verts[mf.sharedMesh] = v = mf.sharedMesh.vertices;
                }
                foreach (var x in v)
                {
                    Vector3 q = m.MultiplyPoint3x4(x);
                    float d = Solid(q);
                    if (d > max) { max = d; part = r.name; at = q; }
                    if (d > core && !r.name.StartsWith("Elbow") && !r.name.StartsWith("Shoulder")) { core = d; corePart = r.name; }
                }
            }
            return max;
        }

        /// <summary>
        /// How deep a table-space point is inside the table's solid parts: the rails and cushions (from the cloth up to the
        /// rail top, round the playing area), the bed under the cloth, the apron walls under the rails (straight walls;
        /// the open corners round the pockets and the space under the rails' overhang are free).
        /// </summary>
        static float Solid(Vector3 q)
        {
            float ax = Mathf.Abs(q.x), az = Mathf.Abs(q.z), y = q.y;
            if (ax > PoolTableGeom.OutX || az > PoolTableGeom.OutZ || y > PoolDims.RailTopY) return 0f;
            bool play = ax < PoolTableGeom.HalfL && az < PoolTableGeom.HalfW;
            if (y >= PoolDims.ClothY)
                return play ? 0f : Mathf.Min(PoolDims.RailTopY - y, Mathf.Min(PoolTableGeom.OutX - ax, PoolTableGeom.OutZ - az));
            if (ax < PoolTableGeom.BedX && az < PoolTableGeom.BedZ && y > PoolDims.ClothY - PoolTableGeom.SlateT)
                return Mathf.Min(PoolDims.ClothY - y, 0.03f);
            float bottom = PoolDims.ClothY - PoolTableGeom.ApronH;
            if (y < bottom) return 0f;
            float wx = Mathf.Abs(ax - (PoolTableGeom.FrameHX - 0.007f)), wz = Mathf.Abs(az - (PoolTableGeom.FrameHZ - 0.007f));
            float d = 0f;
            if (wx < 0.024f && az < PoolTableGeom.FrameHZ - 0.25f) d = 0.024f - wx;
            if (wz < 0.024f && ax < PoolTableGeom.FrameHX - 0.3f) d = Mathf.Max(d, 0.024f - wz);
            return d;
        }

        /// <summary>The own board camera: the own body never in view, nobody else drawn within a metre in front of it.</summary>
        void CameraCheck(PoolTable pool, int place, ref int own, ref int occl, ref string who)
        {
            var me = NetPlayer.Local;
            if (me == null || me.Camera == null) return;
            if (_checkCam == null)
            {
                _checkCam = new GameObject("ProbeCheckCam").AddComponent<Camera>();
                _checkCam.enabled = false;
            }
            pool.CameraPose(place, 1f, out var pos, out var rot);
            _checkCam.transform.SetPositionAndRotation(pos, rot);
            _checkCam.fieldOfView = me.Camera.fieldOfView;
            _checkCam.aspect = me.Camera.aspect;
            _checkCam.nearClipPlane = me.Camera.nearClipPlane;
            var planes = GeometryUtility.CalculateFrustumPlanes(_checkCam);
            // The first-person arms have their own camera, drawn over everything: never on at the table.
            var vmCam = (me.ViewModel as HideAndSeek.Characters.FirstPersonViewModel)?.ViewModelCamera;
            if (vmCam != null && vmCam.enabled) { own++; if (who.Length < 120) who += "[first-person arms]"; }
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || pl.Character == null) continue;
                foreach (var r in pl.Character.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    if (pl.IsOwner) { own++; if (who.Length < 120) who += $"[own {r.name}]"; break; }
                    if (Mathf.Sqrt(r.bounds.SqrDistance(pos)) < 1.0f)
                    {
                        occl++;
                        if (who.Length < 120) who += $"[{pl.Nick} {r.name}]";
                        break;
                    }
                }
            }
        }

        /// <summary>Host: every inning (a team coming to the table) - who plays it; a team's two players must take turns.</summary>
        IEnumerator PoolRotationWatch(PoolTable pool)
        {
            int lastTeam = -1, lastShot = int.MinValue, round = -1;
            var lastOf = new[] { -1, -1 };
            var membersAt = new[] { 0, 0 };
            while (true)
            {
                var s = pool.State;
                if (s.Phase == PoolState.Playing && !pool.Rolling && s.ShotNo != lastShot)
                {
                    lastShot = s.ShotNo;
                    if (s.Round != round) { round = s.Round; lastTeam = -1; lastOf[0] = lastOf[1] = -1; }
                    int team = s.Turn, shooter = s.Shooter, members = s.Members(team);
                    if (team != lastTeam)
                    {
                        bool ok = !(members == 2 && membersAt[team] == 2 && lastOf[team] == shooter) && PoolState.TeamOf(shooter) == team;
                        Log($"inning: team {(team == 0 ? 'A' : 'B')} plays it with place {shooter} ({pool.SeatNick(shooter)}), members {members}, last time place {lastOf[team]} => {(ok ? "OK" : "FAIL")}");
                        lastOf[team] = shooter;
                        membersAt[team] = members;
                        lastTeam = team;
                    }
                }
                yield return null;
            }
        }
    }
}
