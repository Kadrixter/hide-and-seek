using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Paint of the five track karts (slot order from the gate): red #7 (the original), purple #3, burgundy #5,
    /// black #9 (red stripes), blue #1. Body paint is glossy; materials are cached per livery.
    /// </summary>
    public readonly struct KartLivery
    {
        public readonly string Name;
        public readonly Color Body, Stripe;
        public readonly int Number;

        public KartLivery(string name, Color body, Color stripe, int number)
        {
            Name = name;
            Body = body;
            Stripe = stripe;
            Number = number;
        }

        private static readonly KartLivery[] All =
        {
            new("czerwony", new Color(0.82f, 0.07f, 0.05f), new Color(0.93f, 0.93f, 0.9f), 7),
            new("fioletowy", new Color(0.4f, 0.1f, 0.62f), new Color(0.93f, 0.93f, 0.9f), 3),
            new("bordowy", new Color(0.36f, 0.025f, 0.07f), new Color(0.93f, 0.85f, 0.55f), 5),
            new("czarny", new Color(0.035f, 0.035f, 0.04f), new Color(0.85f, 0.08f, 0.06f), 9),
            new("niebieski", new Color(0.05f, 0.22f, 0.78f), new Color(0.93f, 0.93f, 0.9f), 1),
            // two-seaters (28.11)
            new("zielony dwuosobowy", new Color(0.06f, 0.5f, 0.16f), new Color(0.95f, 0.85f, 0.2f), 22),
            new("pomarańczowy dwuosobowy", new Color(0.95f, 0.42f, 0.04f), new Color(0.06f, 0.06f, 0.07f), 44),
            new("biały dwuosobowy", new Color(0.9f, 0.9f, 0.88f), new Color(0.05f, 0.22f, 0.78f), 88),
        };

        public static KartLivery Of(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];

        private static readonly Dictionary<string, Material> Mats = new();

        public Material BodyMat => Get("KartBody_" + Name, Body, 0.8f);
        public Material StripeMat => Get("KartStripe_" + Name, Stripe, 0.5f);

        private static Material Get(string key, Color c, float gloss)
        {
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = key, color = c };
            m.SetFloat("_Glossiness", gloss);
            Mats[key] = m;
            return m;
        }
    }
}
