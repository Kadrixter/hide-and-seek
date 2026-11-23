using System.Linq;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Test switch -testwreck &lt;folder&gt; (host, round running, best with -flashlight): walks the host round the
    /// wrecked Golf GTI on level -2 (front, both sides, the writing, the parts), takes the player's view and a torch-lit
    /// camera shot at each stop, and checks the colliders (a capsule inside the body must be blocked). Quits when done.
    /// </summary>
    public partial class NetSession
    {
        private System.Collections.IEnumerator WreckTour(string dir)
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading || NetPlayer.Local == null)
            {
                mm = MatchManager.Instance;
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(2f);
            var wreck = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
                .Select(r => r.transform).FirstOrDefault(t => t.name.StartsWith("Wreck_VwGolf4Gti"));
            if (wreck == null)
            {
                Debug.LogError("[WreckTour] wreck not found");
                Application.Quit();
                yield break;
            }
            var host = NetPlayer.Local;
            Vector3 W(float x, float y, float z) => wreck.TransformPoint(new Vector3(x, y, z));
            bool blocked = Physics.CheckCapsule(W(0f, 0.5f, 0f), W(0f, 0.9f, 0f), 0.3f, ~0, QueryTriggerInteraction.Ignore);
            var mf = wreck.GetComponent<MeshFilter>();
            Debug.Log($"[WreckTour] {wreck.name} at {wreck.position} yaw {wreck.eulerAngles.y:0.0}, layer {wreck.gameObject.layer}, " +
                      $"{mf.sharedMesh.vertexCount} verts, colliders {wreck.GetComponents<BoxCollider>().Length}, body blocks capsule: {blocked}, static {wreck.gameObject.isStatic}");
            // (name, stand point, look-at) in the wreck's local frame
            var stops = new (string n, Vector3 stand, Vector3 at)[]
            {
                ("a_front", new Vector3(0.6f, 0f, 5.2f), new Vector3(0.1f, 0.6f, 1.2f)),
                ("b_front_right", new Vector3(3.0f, 0f, 3.6f), new Vector3(0.3f, 0.6f, 0.6f)),
                ("c_right", new Vector3(3.2f, 0f, -0.6f), new Vector3(0.6f, 0.7f, -0.6f)),
                ("d_left_door", new Vector3(-2.6f, 0f, 2.4f), new Vector3(-0.5f, 0.75f, 0.3f)),
                ("e_rear", new Vector3(-1.4f, 0f, -4.2f), new Vector3(0f, 0.7f, -1f)),
            };
            foreach (var (n, standL, atL) in stops)
            {
                Vector3 stand = W(standL.x, 0.05f, standL.z), at = W(atL.x, atL.y, atL.z);
                Vector3 eye = stand + Vector3.up * 1.6f, look = at - eye;
                float yaw = Quaternion.LookRotation(new Vector3(look.x, 0, look.z)).eulerAngles.y;
                float pitch = Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                host.Motor.Teleport(stand, yaw, pitch);
                host.Motor.SetLook(yaw, pitch);
                yield return new WaitForSeconds(1.2f);
                Shot(dir, n + "_player");
                yield return null;
                LitShot(dir, n + "_lit", eye, at);
                yield return new WaitForSeconds(0.4f);
                Debug.Log($"[WreckTour] {n}: stood at {host.transform.position}");
            }
            // close-ups: the writing, the engine bay, the interior through the open door
            LitShot(dir, "f_writing", W(2.3f, 1.05f, -0.75f), W(0.86f, 0.78f, -0.7f));
            LitShot(dir, "g_engine", W(0.3f, 1.9f, 3.0f), W(0f, 0.55f, 1.45f));
            LitShot(dir, "h_interior", W(-1.3f, 1.35f, -0.4f), W(0.2f, 0.72f, 0.4f));
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[WreckTour] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
