using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        bool brickOn;
        Color brickColor = Color.white;
        float pokeT = 9f;
        Vector3 pokeLocal;

        /// <summary>Lobby building: a toy brick of <paramref name="color"/> in the right hand (null = none).</summary>
        public void SetBrick(Color? color)
        {
            EnsureInit();
            brickOn = color.HasValue;
            if (color.HasValue) brickColor = color.Value;
        }

        /// <summary>Right hand reaches out and puts a brick down at <paramref name="world"/>.</summary>
        public void PlayPoke(Vector3 world)
        {
            EnsureInit();
            pokeT = 0f;
            pokeLocal = transform.InverseTransformPoint(world);
        }

        const float PokeDuration = 0.4f;

        void ApplyPoke(ref CharacterPose p, float dt, bool standing)
        {
            pokeT += dt;
            if (pokeT >= PokeDuration || !standing) return;
            float w = Mathf.Sin(pokeT / PokeDuration * Mathf.PI);
            var ap = p;
            // Reach towards the spot, no further than the arm allows (the IK clamps), leaning in a little.
            Vector3 target = pokeLocal + new Vector3(0f, 0.04f, -0.02f);
            ap.Torso = p.Torso * Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(target.y - 1.1f, Mathf.Max(0.3f, target.z)) * Mathf.Rad2Deg * 0.4f, -10f, 30f), 0f, 0f);
            ap.SolveArm(0, target, TorsoDir(ap, 1f, -0.6f, -0.4f));
            CharacterPose.LerpArm(ref p, ap, 0, w);
            p.Torso = Quaternion.Slerp(p.Torso, ap.Torso, w);
        }
    }
}
