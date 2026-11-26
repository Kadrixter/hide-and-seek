using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Polls <see cref="PadInput"/> once per frame before every other script (created automatically, survives scene
    /// loads), stops the rumble on quit and re-adds the idle fallback keyboard / mouse if the last real one goes away.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PadInputRunner : MonoBehaviour
    {
        private static PadInputRunner _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("PadInput");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PadInputRunner>();
            PadInput.EnsureFallbackDevices();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private static void OnDeviceChange(InputDevice d, InputDeviceChange c)
        {
            if (c == InputDeviceChange.Removed && (d is Keyboard || d is Mouse)) PadInput.EnsureFallbackDevices();
            if (d is Gamepad && (c == InputDeviceChange.Added || c == InputDeviceChange.Removed))
                Debug.Log($"[Pad] {c}: {d.displayName} ({d.layout})");
        }

        private void Update() => PadInput.Tick();

        private void OnApplicationQuit() => PadInput.StopRumble();

        private void OnDestroy()
        {
            PadInput.StopRumble();
            if (_instance == this) _instance = null;
        }
    }
}
