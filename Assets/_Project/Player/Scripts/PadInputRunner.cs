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
            LogDevices("start");
        }

        /// <summary>
        /// Writes every input device Unity sees (and the HID devices it could not match to a layout) to the player log,
        /// so a pad that is not picked up can be diagnosed from Player.log.
        /// </summary>
        public static void LogDevices(string why)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Pad] devices ({why}):");
            foreach (var d in InputSystem.devices)
                sb.Append($"\n  {d.name} | layout {d.layout} | {d.description.interfaceName} | {d.description.manufacturer} {d.description.product} | enabled {d.enabled}");
            var unsupported = new System.Collections.Generic.List<UnityEngine.InputSystem.Layouts.InputDeviceDescription>();
            InputSystem.GetUnsupportedDevices(unsupported);
            foreach (var u in unsupported)
                sb.Append($"\n  UNSUPPORTED: {u.interfaceName} | {u.manufacturer} {u.product} | {u.deviceClass} | caps {(u.capabilities != null && u.capabilities.Length > 200 ? u.capabilities.Substring(0, 200) : u.capabilities)}");
            Debug.Log(sb.ToString());
        }

        private static void OnDeviceChange(InputDevice d, InputDeviceChange c)
        {
            if (c == InputDeviceChange.Removed && (d is Keyboard || d is Mouse)) PadInput.EnsureFallbackDevices();
            if (c == InputDeviceChange.Added || c == InputDeviceChange.Removed || c == InputDeviceChange.Disconnected || c == InputDeviceChange.Reconnected)
                Debug.Log($"[Pad] {c}: {d.displayName} ({d.layout}, {d.description.interfaceName}, {d.description.product})");
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
