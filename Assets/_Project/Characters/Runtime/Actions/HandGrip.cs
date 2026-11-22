using System;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// How an inventory item sits in the right hand while its hotbar slot is selected: the model (the same factory the
    /// dropped / map item uses, so the hand and the floor always show one model), which point of it is in the fist, and
    /// where the fist goes in third person (body space) and in first person (camera space). Used by
    /// <see cref="BlockyCharacterActions.SetCarried"/> and <see cref="FirstPersonViewModel.SetCarried"/>; switching
    /// lowers the old item, swaps it at the bottom and raises the new one. Items with their own hand animation
    /// (seeker weapons through HeldItem, the toy gun, the RC transmitter) do not use it.
    /// <para>
    /// Spaces: model = the space of <see cref="Model"/>'s root; body = torso-local (x right, y up from the hips, z
    /// forward, shoulders at y 0.56, x +-0.34, a hanging fist at about (0.34, 0.06, 0)); camera = view-model rig
    /// (x right, y up, z forward from the eye, right shoulder at (0.28, -0.52, 0.35), arm reach about 0.5 m).
    /// </para>
    /// </summary>
    public sealed class HandGrip
    {
        /// <summary>Builds the model under the given parent (any layer: the hand sets layer, shadows, removes colliders).</summary>
        public Func<Transform, GameObject> Model;

        /// <summary>Point of the model (model space, before <see cref="Scale"/>) that sits in the centre of the fist.</summary>
        public Vector3 Grip;
        /// <summary>Model scale in the third-person hand (and the base for first person).</summary>
        public float Scale = 1f;

        // ---------------------------------------------------------------- third person

        /// <summary>
        /// True = carried at the side in a hanging arm (swinging with the walk, like a bag); <see cref="Rotation"/> is
        /// then in character-root space. False = the fist goes to <see cref="Hand"/> and the model turns to
        /// <see cref="Rotation"/> in body space.
        /// </summary>
        public bool Hang;
        /// <summary>Fist position, body space (not used with <see cref="Hang"/>).</summary>
        public Vector3 Hand = new Vector3(0.24f, 0.28f, 0.28f);
        /// <summary>Model rotation (Euler), body space (root space with <see cref="Hang"/>).</summary>
        public Vector3 Rotation;
        /// <summary>0..1 how much the hold tilts with the look pitch.</summary>
        public float Look;
        /// <summary>Left fist on the model (model space) for two-handed carrying, null = left arm free.</summary>
        public Vector3? Left;

        // ---------------------------------------------------------------- first person

        /// <summary>Point of the model in the fist in first person (null = <see cref="Grip"/>). There the forearm comes
        /// up from below, so an item that hangs under the fist in third person is held by its side instead.</summary>
        public Vector3? FpGrip;
        /// <summary>Fist position in camera space (keep it low and right so the item never covers the crosshair).</summary>
        public Vector3 FpHand = new Vector3(0.28f, -0.34f, 0.6f);
        /// <summary>Model rotation (Euler), camera space.</summary>
        public Vector3 FpRotation;
        /// <summary>Extra scale in first person (on top of <see cref="Scale"/>).</summary>
        public float FpScale = 0.8f;
        /// <summary>Left fist on the model in first person (model space), null = left arm out of view.</summary>
        public Vector3? FpLeft;

        /// <summary>Grip point used in first person.</summary>
        public Vector3 FpGripPoint => FpGrip ?? Grip;

        /// <summary>Rotation that points the model's <paramref name="axis"/> along <paramref name="dir"/> with the model's
        /// <paramref name="up"/> as close to <paramref name="upDir"/> as possible (Euler, for the fields above).</summary>
        public static Vector3 Aim(Vector3 axis, Vector3 dir, Vector3 up, Vector3 upDir)
        {
            Quaternion model = Quaternion.LookRotation(axis, up);
            Quaternion want = Quaternion.LookRotation(dir, upDir);
            return (want * Quaternion.Inverse(model)).eulerAngles;
        }

        /// <summary>Builds the model under <paramref name="socket"/> at the grip point <paramref name="grip"/>: pivot with
        /// the grip offset and scale, model layer set, colliders / lights / rigidbodies removed, shadows as given.
        /// Returns the pivot.</summary>
        public Transform Build(Transform socket, Vector3 grip, int layer, float scale, UnityEngine.Rendering.ShadowCastingMode shadows, bool receiveShadows)
        {
            var pivot = new GameObject("Carried").transform;
            pivot.gameObject.layer = layer;
            pivot.SetParent(socket, false);
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = -(grip * scale);
            GameObject go = null;
            try { go = Model?.Invoke(pivot); }
            catch (Exception e) { Debug.LogException(e); }
            if (go != null && go.transform.parent != pivot && go.transform != pivot) go.transform.SetParent(pivot, false);
            foreach (var c in pivot.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false; // never pushes the own character controller, not even this frame
                UnityEngine.Object.Destroy(c);
            }
            foreach (var rb in pivot.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(rb);
            foreach (var l in pivot.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows;
                r.receiveShadows = receiveShadows;
            }
            return pivot;
        }
    }
}
