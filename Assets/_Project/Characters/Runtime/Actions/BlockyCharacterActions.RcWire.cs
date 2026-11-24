using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Third person of the fence wire (RC antenna upgrade): tearing a piece of wire out of a wire-mesh fence (both fists
    /// on one strand, bending it back and forth with the body, a jolt back when it snaps, a look at it, into the
    /// pocket) and swapping the RC transmitter's antenna (<see cref="AntennaSwapTimeline"/>: the left hand pulls the
    /// chrome antenna out and throws it away - it flies and lies on the floor for a while - takes the wire from the
    /// pocket and pushes it into the socket). The antenna look of the transmitter itself is set by the game
    /// (<see cref="RcWireTracker"/>); the loose pieces in the hand are drawn here.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float wirePullT = -1f, wirePullW, antSwapT = -1f, antSwapW, antSwapPrevT = -1f;
        Vector3 wireGripR, wireGripL;
        Transform swapOld, swapWire;
        Vector3 swapOldVel, swapOldSpin, prevLeftWorld;
        float swapOldFloor, swapOldBorn = -1f;
        bool swapOldFlying;

        /// <summary>Tearing wire: time on <see cref="WirePullTimeline"/> (&lt; 0 = off) and the two fist points on the mesh (world).</summary>
        public void SetWirePull(float t, Vector3 gripRightWorld, Vector3 gripLeftWorld)
        {
            EnsureInit();
            wirePullT = t;
            if (t < 0f) return;
            wireGripR = transform.InverseTransformPoint(gripRightWorld);
            wireGripL = transform.InverseTransformPoint(gripLeftWorld);
        }

        /// <summary>Antenna swap: time on <see cref="AntennaSwapTimeline"/> (&lt; 0 = off).</summary>
        public void SetAntennaSwap(float t) => antSwapT = t;

        public bool WirePulling => wirePullT >= 0f && wirePullT < WirePullTimeline.Duration;

        /// <summary>Right fist (world), e.g. for the wire torn off the fence.</summary>
        public Vector3 RightHandWorld => socketR != null ? socketR.position : transform.position + Vector3.up;

        /// <summary>Left fist (world).</summary>
        public Vector3 LeftHandWorld => character != null && character.ElbowLeft != null
            ? character.ElbowLeft.TransformPoint(CharacterPose.HandLocal) : transform.position + Vector3.up;

        void ApplyRcWireLayers(ref CharacterPose p, float dt, bool standing, float lookPitch)
        {
            ApplyWirePullLayer(ref p, dt, standing);
            ApplyAntennaSwapLayer(ref p, dt, standing);
            TickSwapPieces(dt);
        }

        // ================================================================ tearing wire off the fence

        void ApplyWirePullLayer(ref CharacterPose p, float dt, bool standing)
        {
            bool on = WirePulling && standing;
            wirePullW = Damp(wirePullW, on ? 1f : 0f, 14f, dt);
            if (!on || wirePullW < 0.001f) return;
            var keep = p.PropR;
            var wp = WirePullPose(p, wirePullT);
            p = CharacterPose.Lerp(p, wp, WirePullTimeline.Body(wirePullT) * wirePullW);
            p.PropR = keep;
        }

        CharacterPose WirePullPose(in CharacterPose cur, float t)
        {
            float S(float a, float b) => WirePullTimeline.S(t, a, b);
            var q = cur;
            float reach = S(0.05f, WirePullTimeline.Reach);
            float bend = WirePullTimeline.Bend(t);
            float strain = S(WirePullTimeline.Reach, WirePullTimeline.Reach + 0.3f) * (1f - S(WirePullTimeline.Snap, WirePullTimeline.Snap + 0.1f));
            float jolt = t > WirePullTimeline.Snap ? Mathf.Exp(-(t - WirePullTimeline.Snap) * 7f) : 0f;
            float after = S(WirePullTimeline.Snap, WirePullTimeline.Snap + 0.3f);
            float shake = (Mathf.Sin(t * 37f) * 0.6f + Mathf.Sin(t * 23f) * 0.4f) * strain;

            // Lean in to the mesh, rock with every bend, jolt back on the snap.
            q.Pelvis = cur.Pelvis * Quaternion.Euler(3f * reach * (1f - after), 0f, 0f);
            q.Torso = cur.Torso * Quaternion.Euler(9f * reach * (1f - after) + 4f * bend * strain - 12f * jolt + 0.8f * shake, -7f * bend * strain, 2.5f * bend * strain);
            q.Head = cur.Head * Quaternion.Euler(16f * reach * (1f - after) + 22f * after * (1f - S(WirePullTimeline.Pocket - 0.2f, WirePullTimeline.Duration)), 0f, 0f);
            q.PelvisPos = cur.PelvisPos + new Vector3(0f, -0.03f * strain, 0.03f * reach - 0.05f * jolt);

            // Right fist: on the strand, bending it towards / away from the body; after the snap up to the chest
            // (looking at the piece), then into the right pocket.
            Vector3 r = wireGripR + new Vector3(0.01f * bend, 0.04f * bend, -0.045f * Mathf.Abs(bend)) * strain;
            Vector3 back = wireGripR + new Vector3(0.04f, 0.02f, -0.18f);
            Vector3 chest = q.TorsoPoint(new Vector3(0.05f, 0.44f, 0.32f));
            Vector3 pocket = q.TorsoPoint(new Vector3(0.26f, -0.02f, 0.06f));
            if (t >= WirePullTimeline.Snap)
            {
                r = Vector3.Lerp(Vector3.Lerp(wireGripR, back, S(WirePullTimeline.Snap, WirePullTimeline.Snap + 0.12f)), chest, S(WirePullTimeline.Snap + 0.1f, WirePullTimeline.Snap + 0.35f));
                r = Vector3.Lerp(r, pocket, S(WirePullTimeline.Pocket - 0.3f, WirePullTimeline.Pocket + 0.05f));
            }
            Vector3 hr = Vector3.Lerp(cur.HandPos(0), r, reach * (1f - S(WirePullTimeline.Pocket + 0.05f, WirePullTimeline.Duration)));
            // Left fist: holds the mesh still until the snap, then lets go.
            Vector3 l = wireGripL + new Vector3(0f, 0f, 0.004f * shake);
            Vector3 hl = Vector3.Lerp(cur.HandPos(1), l, reach * (1f - S(WirePullTimeline.Snap, WirePullTimeline.Snap + 0.4f)));
            q.SolveArm(0, hr, TorsoDir(q, 1f, -0.75f, -0.35f));
            q.SolveArm(1, hl, TorsoDir(q, -1f, -0.75f, -0.35f));
            return q;
        }

        // ================================================================ antenna swap

        void ApplyAntennaSwapLayer(ref CharacterPose p, float dt, bool standing)
        {
            float t = antSwapT;
            bool on = AntennaSwapTimeline.Running(t) && standing && shownItem == HeldItem.RcRemote;
            antSwapW = Damp(antSwapW, on ? 1f : 0f, 16f, dt);
            if (antSwapW < 0.001f) return;
            t = Mathf.Clamp(t, 0f, AntennaSwapTimeline.End);
            float lifted = AntennaSwapTimeline.Lifted(t) * antSwapW;

            // The transmitter: a little higher and nearer, rolled so the antenna corner comes up to the left hand.
            Quaternion rot0 = p.ForearmRoot(0) * p.PropR;
            Vector3 hand0 = p.HandPos(0);
            Vector3 hand = hand0 + p.TorsoRoot * new Vector3(-0.03f, 0.08f, -0.02f) * lifted;
            Quaternion rot = rot0 * Quaternion.Euler(-10f * lifted, 0f, -14f * lifted);
            p.SolveArm(0, hand, TorsoDir(p, 1f, -0.9f, -0.4f));
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * rot;

            // The left hand does the work.
            hand = p.HandPos(0);
            Vector3 toss = p.TorsoPoint(new Vector3(-0.5f, 0.5f, 0.24f));
            Vector3 pocket = p.TorsoPoint(new Vector3(-0.27f, -0.03f, 0.05f));
            Vector3 left = AntennaSwapTimeline.LeftHand(t, hand, rot, toss, pocket);
            p.SolveArm(1, Vector3.Lerp(p.HandPos(1), left, antSwapW), TorsoDir(p, -1f, -0.8f, -0.3f));
            p.Head = p.Head * Quaternion.Euler(12f * lifted, -6f * lifted, 0f);

            SwapPieces(t, p, hand, rot);
        }

        /// <summary>The thrown-away chrome antenna and the wire in the left hand (world objects, character layer).</summary>
        void SwapPieces(float t, in CharacterPose p, Vector3 handRoot, Quaternion rotRoot)
        {
            float prev = antSwapPrevT;
            antSwapPrevT = t;
            int layer = gameObject.layer;
            var shadows = character != null && character.IsFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            Vector3 leftW = transform.TransformPoint(p.HandPos(1));
            Quaternion propW = transform.rotation * rotRoot;
            Vector3 propPos = transform.TransformPoint(handRoot);

            // Old antenna: out of the socket into the left fist, thrown away at Release.
            if (prev < AntennaSwapTimeline.YankEnd && t >= AntennaSwapTimeline.YankEnd && t < AntennaSwapTimeline.Release)
            {
                DestroySwapOld();
                swapOld = new GameObject("RcAntennaOld").transform;
                swapOld.gameObject.layer = layer;
                RcRemoteModel.BuildAntenna(swapOld, layer);
                Destroy(swapOld.GetChild(0).gameObject); // the socket stays on the transmitter
                swapOldFlying = false;
                swapOldBorn = Time.time;
            }
            if (swapOld != null && !swapOldFlying)
            {
                if (t < AntennaSwapTimeline.Release)
                {
                    // Held by the mast a bit above its middle, pointing up and out of the fist.
                    Quaternion r = Quaternion.Slerp(propW * AntennaSwapTimeline.AntRot, transform.rotation * Quaternion.Euler(10f, 0f, 55f),
                        AntennaSwapTimeline.S(t, AntennaSwapTimeline.YankEnd, AntennaSwapTimeline.Release));
                    swapOld.SetPositionAndRotation(leftW - r * new Vector3(0f, AntennaSwapTimeline.StockGrip, 0f), r);
                }
                else
                {
                    swapOldFlying = true;
                    float dtv = Mathf.Max(Time.deltaTime, 0.01f);
                    swapOldVel = (leftW - prevLeftWorld) / dtv * 0.6f + transform.rotation * new Vector3(-1.6f, 1.3f, 0.6f);
                    swapOldSpin = new Vector3(Random.Range(-700f, 700f), Random.Range(-300f, 300f), Random.Range(-700f, 700f));
                    swapOldFloor = transform.position.y;
                    if (Physics.Raycast(swapOld.position + Vector3.up * 0.2f, Vector3.down, out var hit, 3f, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 13)), QueryTriggerInteraction.Ignore))
                        swapOldFloor = hit.point.y;
                }
            }
            prevLeftWorld = leftW;

            // New wire: out of the pocket, up to the socket, pushed in; the transmitter shows its own wire from Install.
            bool wire = AntennaSwapTimeline.WireInHand(t);
            if (wire && swapWire == null)
            {
                swapWire = WireModel.CreateLoose(null, layer);
                swapWire.name = "RcWireInHand";
            }
            if (swapWire != null && !wire) { Destroy(swapWire.gameObject); swapWire = null; }
            if (swapWire != null)
            {
                Quaternion axisRot = propW * AntennaSwapTimeline.AntRot;
                Quaternion hang = transform.rotation * Quaternion.Euler(150f, 0f, 20f);
                Vector3 pos;
                Quaternion r;
                if (t < AntennaSwapTimeline.BringEnd)
                {
                    r = Quaternion.Slerp(hang, axisRot, AntennaSwapTimeline.S(t, AntennaSwapTimeline.Pocket + 0.15f, AntennaSwapTimeline.BringEnd));
                    pos = leftW - r * new Vector3(0f, AntennaSwapTimeline.WireGrip, 0f);
                }
                else
                {
                    r = axisRot;
                    pos = propPos + propW * AntennaSwapTimeline.OnAxis(AntennaSwapTimeline.WireLift(t));
                }
                swapWire.SetPositionAndRotation(pos, r);
            }
            SetPieceRender(swapOld, layer, shadows);
            SetPieceRender(swapWire, layer, shadows);
        }

        static void SetPieceRender(Transform t, int layer, ShadowCastingMode mode)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.layer != layer) r.gameObject.layer = layer;
                if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
        }

        /// <summary>Every frame: the thrown antenna flies, bounces once and lies on the floor for a while.</summary>
        void TickSwapPieces(float dt)
        {
            if (!AntennaSwapTimeline.Running(antSwapT))
            {
                antSwapPrevT = -1f;
                if (swapWire != null) { Destroy(swapWire.gameObject); swapWire = null; }
                if (swapOld != null && !swapOldFlying) DestroySwapOld(); // interrupted before the throw
            }
            if (swapOld == null || !swapOldFlying) return;
            if (Time.time - swapOldBorn > 20f) { DestroySwapOld(); return; }
            if (swapOldVel.sqrMagnitude < 1e-4f) return;
            swapOldVel += Physics.gravity * dt;
            Vector3 pos = swapOld.position + swapOldVel * dt;
            Quaternion rot = Quaternion.Euler(swapOldSpin * dt) * swapOld.rotation;
            if (pos.y <= swapOldFloor + 0.006f)
            {
                pos.y = swapOldFloor + 0.006f;
                if (Mathf.Abs(swapOldVel.y) > 1.2f)
                {
                    swapOldVel = new Vector3(swapOldVel.x * 0.4f, -swapOldVel.y * 0.25f, swapOldVel.z * 0.4f);
                    swapOldSpin *= 0.3f;
                }
                else
                {
                    swapOldVel = Vector3.zero;
                    // lies flat on the floor
                    Vector3 up = rot * Vector3.up;
                    Vector3 along = new Vector3(up.x, 0f, up.z);
                    if (along.sqrMagnitude < 1e-4f) along = transform.right;
                    rot = Quaternion.FromToRotation(up, along.normalized) * rot;
                }
            }
            swapOld.SetPositionAndRotation(pos, rot);
        }

        void DestroySwapOld()
        {
            if (swapOld != null) Destroy(swapOld.gameObject);
            swapOld = null;
            swapOldFlying = false;
        }

        /// <summary>World position of the thrown-away antenna (tests), null when there is none.</summary>
        public Vector3? ThrownAntenna => swapOld != null && swapOldFlying ? swapOld.position : (Vector3?)null;
    }
}
