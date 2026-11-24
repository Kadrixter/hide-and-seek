using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First person of the RC antenna swap (<see cref="AntennaSwapTimeline"/>): the transmitter lifted in both hands
    /// with its antenna corner up in view; the left hand pulls the chrome antenna out and throws it away to the left
    /// (out of view), comes back from the pocket with the piece of wire and pushes it into the socket, twists it round,
    /// back on the handle. The transmitter's own antenna look is set by the game (<see cref="RcWireTracker"/>).
    /// </summary>
    public partial class FirstPersonViewModel
    {
        float vmSwapT = -1f, vmSwapPrevT = -1f, vmOldFlyT = -1f;
        Transform vmSwapOld, vmSwapWire;
        Vector3 vmOldVel, vmOldSpin, vmPrevLeft;

        /// <summary>Antenna swap time (&lt; 0 = off).</summary>
        public void SetAntennaSwap(float t) => vmSwapT = t;

        bool AntennaSwapHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            float t = vmSwapT;
            if (!AntennaSwapTimeline.Running(t) || rig == null || (show != HeldItem.None && show != HeldItem.RcRemote) || !vmRemoteWanted)
            {
                VmSwapCleanup();
                return false;
            }
            float lifted = AntennaSwapTimeline.Lifted(t);
            float breath = Mathf.Sin(time * 1.7f);
            rightT = RemoteHoldPos + new Vector3(0.05f, 0.15f + 0.003f * breath, -0.02f) * lifted;
            propRot = RemoteHoldRot * Quaternion.Euler(-14f * lifted, 0f, 10f * lifted);
            show = HeldItem.RcRemote;
            vmRemoteRaise = 1f;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.5f));
            Vector3 hand = HandPos(shR, elR);
            Vector3 toss = new Vector3(-0.55f, -0.12f, 0.5f), pocket = new Vector3(-0.34f, -0.95f, 0.32f);
            leftT = AntennaSwapTimeline.LeftHand(t, hand, propRot, toss, pocket);
            wantLeft = 1f;
            VmSwapPieces(t, hand, propRot, leftT);
            return true;
        }

        /// <summary>Loose pieces in rig space on the view-model layer.</summary>
        void VmSwapPieces(float t, Vector3 hand, Quaternion propRot, Vector3 left)
        {
            float prev = vmSwapPrevT;
            vmSwapPrevT = t;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);

            if (prev < AntennaSwapTimeline.YankEnd && t >= AntennaSwapTimeline.YankEnd && t < AntennaSwapTimeline.Release)
            {
                VmDestroyOld();
                vmSwapOld = new GameObject("VmRcAntennaOld").transform;
                vmSwapOld.gameObject.layer = Layer;
                vmSwapOld.SetParent(rig, false);
                RcRemoteModel.BuildAntenna(vmSwapOld, Layer);
                Destroy(vmSwapOld.GetChild(0).gameObject);
                vmOldFlyT = -1f;
            }
            if (vmSwapOld != null)
            {
                if (t < AntennaSwapTimeline.Release)
                {
                    Quaternion r = Quaternion.Slerp(propRot * AntennaSwapTimeline.AntRot, Quaternion.Euler(-10f, 0f, 50f),
                        AntennaSwapTimeline.S(t, AntennaSwapTimeline.YankEnd, AntennaSwapTimeline.Release));
                    vmSwapOld.localPosition = left - r * new Vector3(0f, AntennaSwapTimeline.StockGrip, 0f);
                    vmSwapOld.localRotation = r;
                }
                else
                {
                    if (vmOldFlyT < 0f)
                    {
                        vmOldFlyT = 0f;
                        vmOldVel = (left - vmPrevLeft) / dt * 0.5f + new Vector3(-1.6f, 0.7f, 0.4f);
                        vmOldSpin = new Vector3(Random.Range(-600f, 600f), Random.Range(-200f, 200f), Random.Range(-600f, 600f));
                    }
                    vmOldFlyT += dt;
                    vmOldVel += new Vector3(0f, -6f, 0f) * dt;
                    vmSwapOld.localPosition += vmOldVel * dt;
                    vmSwapOld.localRotation = Quaternion.Euler(vmOldSpin * dt) * vmSwapOld.localRotation;
                    if (vmOldFlyT > 0.6f) VmDestroyOld();
                }
            }
            vmPrevLeft = left;

            bool wire = AntennaSwapTimeline.WireInHand(t);
            if (wire && vmSwapWire == null)
            {
                vmSwapWire = WireModel.CreateLoose(rig, Layer);
                vmSwapWire.name = "VmRcWireInHand";
            }
            if (vmSwapWire != null && !wire) { Destroy(vmSwapWire.gameObject); vmSwapWire = null; }
            if (vmSwapWire != null)
            {
                Quaternion axisRot = propRot * AntennaSwapTimeline.AntRot;
                Quaternion hang = Quaternion.Euler(150f, 0f, -20f);
                if (t < AntennaSwapTimeline.BringEnd)
                {
                    Quaternion r = Quaternion.Slerp(hang, axisRot, AntennaSwapTimeline.S(t, AntennaSwapTimeline.Pocket + 0.15f, AntennaSwapTimeline.BringEnd));
                    vmSwapWire.localRotation = r;
                    vmSwapWire.localPosition = left - r * new Vector3(0f, AntennaSwapTimeline.WireGrip, 0f);
                }
                else
                {
                    vmSwapWire.localRotation = axisRot;
                    vmSwapWire.localPosition = hand + propRot * AntennaSwapTimeline.OnAxis(AntennaSwapTimeline.WireLift(t));
                }
            }
        }

        void VmSwapCleanup()
        {
            vmSwapPrevT = -1f;
            if (vmSwapWire != null) { Destroy(vmSwapWire.gameObject); vmSwapWire = null; }
            VmDestroyOld();
        }

        void VmDestroyOld()
        {
            if (vmSwapOld != null) Destroy(vmSwapOld.gameObject);
            vmSwapOld = null;
            vmOldFlyT = -1f;
        }
    }
}
