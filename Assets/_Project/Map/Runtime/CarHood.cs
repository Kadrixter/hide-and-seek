using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map
{
    /// <summary>
    /// A parked car whose bonnet can be opened (the car-battery objective, Game: MatchManager.CarBatteries). The map
    /// builder (ParkingGarageBuilder.Batteries) splits the bonnet out of the car body into the <see cref="Hood"/> child
    /// (pivot on the hinge at its rear edge, so a rotation about its local X lifts the front), builds a dark engine bay
    /// under it and a car battery (<see cref="Battery"/>) in a corner of the bay. The game drives the visible state:
    /// <see cref="SetOpenAngle"/> and <see cref="SetBatteryVisible"/>. Every peer loads the same scene, so
    /// <see cref="Index"/> means the same car everywhere (<see cref="All"/> is sorted by it).
    /// </summary>
    public sealed class CarHood : MonoBehaviour
    {
        /// <summary>The bonnet (child of the car, pivot on the hinge, +z towards the car's nose).</summary>
        public Transform Hood;
        /// <summary>The battery in the engine bay (hidden once taken).</summary>
        public Transform Battery;
        /// <summary>Garage level: 0 = P 0, 1 = P -1, 2 = P -2.</summary>
        public int Level;
        /// <summary>0..1 how long the car has stood here (dust preset: none 0, Light 0.2, LightPlus 0.4, Heavy 0.7, Forgotten 1).</summary>
        public float Dust;
        /// <summary>Stable index over the whole map (0..n-1, sorted by level and car name at build time).</summary>
        public int Index;
        /// <summary>Car space: middle of the bonnet's front edge (where the latch is).</summary>
        public Vector3 FrontLocal;
        /// <summary>Car space: top middle of the battery.</summary>
        public Vector3 BatteryLocal;
        /// <summary>Fully open angle of the bonnet (degrees).</summary>
        public float OpenAngle = 56f;

        private static readonly List<CarHood> _all = new List<CarHood>();
        private Quaternion _closed = Quaternion.identity;
        private float _angle = -1f;
        private bool _batteryShown = true;

        /// <summary>All car hoods of the loaded map, sorted by <see cref="Index"/>.</summary>
        public static IReadOnlyList<CarHood> All => _all;

        /// <summary>World point of the latch (front edge middle).</summary>
        public Vector3 FrontWorld => transform.TransformPoint(FrontLocal);

        /// <summary>World point on top of the battery.</summary>
        public Vector3 BatteryWorld => transform.TransformPoint(BatteryLocal);

        /// <summary>Current bonnet angle (degrees, 0 = closed).</summary>
        public float Angle => Mathf.Max(0f, _angle);

        private void Awake()
        {
            if (Hood != null) _closed = Hood.localRotation;
            int i = 0;
            while (i < _all.Count && _all[i].Index <= Index) i++;
            _all.Insert(i, this);
        }

        private void OnDestroy() => _all.Remove(this);

        /// <summary>Lifts the bonnet to <paramref name="degrees"/> (0 = closed, <see cref="OpenAngle"/> = fully open).</summary>
        public void SetOpenAngle(float degrees)
        {
            degrees = Mathf.Clamp(degrees, 0f, OpenAngle + 2f);
            if (Hood == null || Mathf.Abs(degrees - _angle) < 0.01f) return;
            _angle = degrees;
            Hood.localRotation = _closed * Quaternion.Euler(-degrees, 0f, 0f);
        }

        public void SetBatteryVisible(bool shown)
        {
            if (Battery == null || shown == _batteryShown) return;
            _batteryShown = shown;
            Battery.gameObject.SetActive(shown);
        }
    }
}
