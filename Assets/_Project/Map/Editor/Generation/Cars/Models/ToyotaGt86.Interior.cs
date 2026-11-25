using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>GT86 cabin seen through the glass: low sport seats, 2+2 rear seats, dash, small wheel, console, mirror.</summary>
    internal sealed partial class ToyotaGt86
    {
        protected override void Interior()
        {
            var cover = Lib["Car_LeatherBlack"];
            var bolster = Lib["Car_LeatherRed"];
            var dash = Lib["Car_Dash"];
            // left-hand drive: driver on -x
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.36f, TubFloor, -0.48f), 0.52f, 0.3f, cover, bolster, true, 20f, 0.6f);
            // 2+2: two shallow rear cushions and an upright backrest against the parcel shelf
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Blob(Mb, new Vector3(s * 0.33f, 0.32f, -1.0f), Quaternion.Euler(-8f, 0, 0), new Vector3(0.21f, 0.05f, 0.17f), 0.35f, cover, 0.35f);
            Mb.Box(new Vector3(0f, 0.55f, -1.2f), new Vector3(1.2f, 0.5f, 0.08f), cover, Quaternion.Euler(-28f, 0, 0));
            Mb.Box(new Vector3(0f, 0.9f, -1.45f), new Vector3(1.2f, 0.02f, 0.5f), Lib["Car_Interior"]); // parcel shelf

            CarInterior.Dash(Mb, 0.67f, 0.44f, 0.1f, 0.8f, 0.45f, -0.36f, dash);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.36f, 0.75f, -0.04f), new Vector3(0f, 0.45f, 1f), 0.18f, cover);
            CarInterior.Console(Mb, -0.85f, 0.12f, TubFloor, 0.25f, dash, Chrome);
            // rear-view mirror hanging from the top of the screen
            Mb.Box(new Vector3(0f, 1.17f, -0.24f), new Vector3(0.22f, 0.06f, 0.025f), dash, Quaternion.Euler(-15f, 0, 0));
            Mb.Box(new Vector3(0f, 1.21f, -0.27f), new Vector3(0.025f, 0.05f, 0.02f), dash);
            // door cards: a padded arm rest along each inner door panel
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(new Vector3(s * (TubWall - 0.03f), 0.62f, -0.35f), new Vector3(0.06f, 0.05f, 0.55f), Lib["Car_Interior"]);
        }
    }
}
