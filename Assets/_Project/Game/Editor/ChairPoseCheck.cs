using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>Batch: solves the office-chair pose and logs the solver trace ([ChairPose]).</summary>
    public static class ChairPoseCheck
    {
        public static void Run()
        {
            var sb = new System.Text.StringBuilder();
            HideAndSeek.Characters.SeatPoses.Trace = sb;
            HideAndSeek.Characters.SeatPoses.UseBaked = false;
            var body = HideAndSeek.Characters.SeatPoses.Body(0, SecurityRooms.ChairShape);
            HideAndSeek.Characters.SeatPoses.Trace = null;
            Debug.Log($"[ChairPose] pelvis {body.PelvisPos:F3}\n{sb}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
