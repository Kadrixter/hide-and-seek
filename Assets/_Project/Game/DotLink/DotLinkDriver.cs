using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Per-frame hook of the dot link panel (lives for the whole session, made at start): the server part of
    /// MatchManager.DotLink, the panel in the exit-gate box once the gate exists (game scene), the local input / view.
    /// Kept outside MatchManager.Update so the escape parts do not all edit the same lines.
    /// </summary>
    public sealed class DotLinkDriver : MonoBehaviour
    {
        float _errorAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("DotLinkDriver");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<DotLinkDriver>();
        }

        void Update()
        {
            try
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.IsSpawned)
                {
                    mm.UpdateDotLink();
                    mm.UpdateArcLink(); // the KABELKI cabinet in the lobby
                }
                DotLinkPanel.Ensure();
                if (NetPlayer.Local != null) DotLinkLocal.Ensure();
            }
            catch (System.Exception e)
            {
                if (Time.unscaledTime - _errorAt < 5f) return;
                _errorAt = Time.unscaledTime;
                Debug.LogException(e);
            }
        }
    }
}
