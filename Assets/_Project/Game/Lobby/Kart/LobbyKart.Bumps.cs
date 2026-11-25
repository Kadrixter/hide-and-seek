using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Kart-to-kart bumps across clients: the driver whose kart hit a driven kart sends that kart's driver the
    /// velocity change it gets (equal and opposite to the own one, at most 4 m/s, a few times a second), so the hit
    /// kart is shoved on its own client too instead of only the hitter bouncing off.
    /// </summary>
    public partial class LobbyKart
    {
        private readonly Dictionary<int, float> _lastPush = new();

        /// <summary>Own kart (local) hit kart <paramref name="other"/>: pass its share of the bump to its driver.</summary>
        public void ReportBump(LobbyKart other, Vector3 dv)
        {
            if (other == null || View != Mode.Local) return;
            if (_lastPush.TryGetValue(other.Index, out float at) && Time.time - at < 0.25f) return;
            _lastPush[other.Index] = Time.time;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            var st = mm.KartsVar.Value.Get(other.Index);
            var driver = st.Driven ? NetPlayer.Find(st.Driver) : null;
            if (driver == null) return;
            dv.y = 0f;
            driver.KartPushRpc((byte)other.Index, Vector3.ClampMagnitude(dv, 4f));
        }

        /// <summary>Driver's client: somebody's kart shoved this one.</summary>
        public void ExternalPush(Vector3 dv)
        {
            if (View != Mode.Local) return;
            Phys.Vel += dv;
            Phys.YawRate += Mathf.Clamp(Vector3.Cross(transform.forward, dv).y * 25f, -60f, 60f);
            Phys.Collide.KartHits++;
            Phys.Collide.Hits++;
            Phys.Collide.Impact = Mathf.Max(Phys.Collide.Impact, dv.magnitude);
            Debug.Log($"[Kart] kart {Index} shoved by another kart: {dv.magnitude:0.00} m/s");
        }
    }
}
