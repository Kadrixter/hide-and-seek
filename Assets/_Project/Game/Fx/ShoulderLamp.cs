using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Blocky flashlight strapped to the right shoulder (Dying Light style). The real spot light is positioned
    /// at the same place relative to the camera (see Flashlight.HoldOffset); this is the visible model, whose
    /// lens glows while the light is on.
    /// </summary>
    public class ShoulderLamp : MonoBehaviour
    {
        /// <summary>Flashlight offset from the eye that matches the model on the shoulder.</summary>
        public static readonly Vector3 EyeOffset = new Vector3(0.13f, -0.38f, 0.17f);

        private Renderer _lens;
        private MaterialPropertyBlock _mpb;
        private bool _on;
        private static Material _body, _lensMat, _strap;

        public static ShoulderLamp Attach(BlockyCharacter character)
        {
            if (character == null || character.Torso == null) return null;
            var root = new GameObject("ShoulderLamp").transform;
            root.SetParent(character.Torso, false);
            // Torso pivot is at the hips; the box is x +-4 px, y 0..12 px, z +-2 px (+z = front, +x = right).
            const float px = BlockyMeshBuilder.Px;
            root.localPosition = new Vector3(2.4f * px, 10.2f * px, 2f * px + 0.03f);
            root.localRotation = Quaternion.identity;
            var lamp = root.gameObject.AddComponent<ShoulderLamp>();
            lamp.Build(root);
            root.gameObject.layer = character.gameObject.layer;
            foreach (Transform t in root) t.gameObject.layer = character.gameObject.layer;
            return lamp;
        }

        private void Build(Transform root)
        {
            if (_body == null)
            {
                _body = new Material(Shader.Find("Standard")) { color = new Color(0.13f, 0.13f, 0.14f) };
                _body.SetFloat("_Metallic", 0.6f);
                _body.SetFloat("_Glossiness", 0.45f);
                _lensMat = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.85f, 0.8f) };
                _lensMat.EnableKeyword("_EMISSION");
                _strap = new Material(Shader.Find("Standard")) { color = new Color(0.05f, 0.05f, 0.05f) };
            }
            Part(root, "Body", new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.07f, 0.11f), _body);
            Part(root, "Head", new Vector3(0f, 0f, 0.085f), new Vector3(0.08f, 0.09f, 0.03f), _body);
            _lens = Part(root, "Lens", new Vector3(0f, 0f, 0.102f), new Vector3(0.06f, 0.07f, 0.006f), _lensMat);
            Part(root, "Clip", new Vector3(0f, 0.045f, 0f), new Vector3(0.03f, 0.02f, 0.08f), _strap);
            // Strap over the shoulder to the back.
            Part(root, "StrapTop", new Vector3(0f, 0.11f, -0.11f), new Vector3(0.035f, 0.012f, 0.27f), _strap);
            Part(root, "StrapFront", new Vector3(0f, 0.06f, -0.0f), new Vector3(0.035f, 0.1f, 0.012f), _strap);
            _mpb = new MaterialPropertyBlock();
            SetOn(false);
        }

        private static Renderer Part(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return r;
        }

        public void SetOn(bool on)
        {
            if (_lens == null || (_on == on && _mpb != null && Time.frameCount > 2)) return;
            _on = on;
            _lens.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.95f, 0.8f) * 3f : Color.black);
            _lens.SetPropertyBlock(_mpb);
        }

        /// <summary>Local player: keep the model out of the first-person view (shadow only).</summary>
        public void SetFirstPerson(bool fp)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = fp ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
