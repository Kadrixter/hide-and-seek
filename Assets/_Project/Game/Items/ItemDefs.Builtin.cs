using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Built-in items. Registration order = slot order when several kinds arrive at once (the seeker's knife first).
    /// Medkits, syringes and rods can also be taken by the seeker (to deny them to the hiders); he cannot use them.
    /// </summary>
    public static partial class ItemDefs
    {
        private static IEnumerable<ItemDef> Builtins()
        {
            yield return new ItemDef
            {
                Item = HotbarItem.Knife, Name = "Nóż", Users = ItemUsers.Seeker, Droppable = false, Weapon = HeldItem.Knife,
                Count = p => p.IsSeeker ? 1 : 0,
                Hint = _ => "F - atak (podstawowa broń, nie do wyrzucenia)",
                Model = r => Prop(r, HeldItem.Knife),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Chainsaw, Name = "Piła łańcuchowa", Users = ItemUsers.Seeker, Weapon = HeldItem.Chainsaw,
                DropSfx = GameSfx.BodyFall, DropVolume = 0.55f, Glow = new Color(1f, 0.6f, 0.25f, 0.5f), SeekerMapMarker = true,
                Count = p => p.HasChainsaw.Value ? 1 : 0,
                Give = (p, _) => { p.HasChainsaw.Value = true; return true; },
                Take = (NetPlayer p, out byte extra) =>
                {
                    extra = 0;
                    if (!p.HasChainsaw.Value) return false;
                    p.HasChainsaw.Value = false;
                    if (p.Held.Value == HeldItem.Chainsaw) p.Held.Value = HeldItem.None;
                    return true;
                },
                Hint = _ => "F przytrzymaj - gaz",
                Model = r => Prop(r, HeldItem.Chainsaw),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Glock, Name = "Glock", Users = ItemUsers.Seeker, Weapon = HeldItem.Glock,
                DropSfx = GameSfx.CanBounce, DropVolume = 0.8f, Glow = new Color(1f, 0.92f, 0.6f, 0.55f), SeekerMapMarker = true,
                Count = p => p.HasGlock.Value ? 1 : 0,
                Give = (p, _) => { p.HasGlock.Value = true; return true; },
                Take = (NetPlayer p, out byte extra) =>
                {
                    extra = 0;
                    var mm = MatchManager.Instance;
                    if (!p.HasGlock.Value || (mm != null && mm.IsReloading(p))) return false;
                    p.HasGlock.Value = false;
                    if (p.Held.Value == HeldItem.Glock) p.Held.Value = HeldItem.None;
                    return true;
                },
                Hint = _ => "LPM - strzał, PPM - celowanie, R - przeładuj",
                Model = r => Prop(r, HeldItem.Glock),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Medkit, Name = "Apteczka", Users = ItemUsers.Both, MaxStack = Tuning.MaxMedkits,
                DropSfx = GameSfx.RcPlace, DropVolume = 0.8f, Glow = new Color(1f, 0.35f, 0.3f, 0.6f),
                Count = p => p.MedkitCount.Value,
                Give = (p, _) => { p.MedkitCount.Value++; return true; },
                Take = (NetPlayer p, out byte extra) => { extra = 0; if (p.MedkitCount.Value == 0) return false; p.MedkitCount.Value--; return true; },
                Hint = MedkitHint,
                Use = UseMedkit,
                Model = r => Prop(r, HeldItem.Medkit),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Syringe, Name = "Adrenalina", Users = ItemUsers.Both, MaxStack = Tuning.MaxSyringes,
                DropSfx = GameSfx.ShellCasing, DropVolume = 0.9f, Glow = new Color(0.4f, 0.75f, 1f, 0.6f),
                Count = p => p.SyringeCount.Value,
                Give = (p, _) => { p.SyringeCount.Value++; return true; },
                Take = (NetPlayer p, out byte extra) => { extra = 0; if (p.SyringeCount.Value == 0) return false; p.SyringeCount.Value--; return true; },
                Hint = _ => Prompt == PromptKind.Resurrect ? "LPM przytrzymaj - wskrześ" : "LPM przy ciele martwego - wskrzesza",
                Use = (p, click, hold) =>
                {
                    if (Prompt == PromptKind.Resurrect) HotbarInput.HoldH = hold;
                    else if (click) Toast("Podejdź do ciała martwego chowającego i przytrzymaj LPM.");
                },
                Model = r => Prop(r, HeldItem.Syringe),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.RcCar, Name = "Samochodzik RC", Users = ItemUsers.Hider,
                DropSfx = GameSfx.RcBump, DropVolume = 0.9f, Glow = new Color(1f, 0.85f, 0.3f, 0.45f),
                Count = p => MatchManager.Instance != null && MatchManager.Instance.RcSlotOf(p) >= 0 ? 1 : 0,
                Give = (p, extra) => MatchManager.Instance != null && MatchManager.Instance.ServerGiveRc(p, extra),
                Take = (NetPlayer p, out byte extra) => { extra = 0; return MatchManager.Instance != null && MatchManager.Instance.ServerTakeRc(p, out extra); },
                Hint = _ => "LPM / 6 - postaw i steruj",
                Use = (p, click, hold) => { if (click) HotbarInput.Six = true; },
                Model = r => BlockyProps.CreateRcCar(r, 0).Root.gameObject,
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Rod, Name = "Pręt", Users = ItemUsers.Both, MaxStack = ItemTuning.MaxRodsCarried,
                DropSfx = GameSfx.CanBounce, DropVolume = 0.9f, Glow = new Color(0.75f, 0.85f, 1f, 0.5f),
                Count = p => p.RodCount.Value,
                Give = (p, _) => { p.RodCount.Value++; return true; },
                Take = (NetPlayer p, out byte extra) => { extra = 0; if (p.RodCount.Value == 0) return false; p.RodCount.Value--; return true; },
                Hint = p => p.FreeStump != 0 ? "LPM - zrób z pręta nogę" : "proteza, gdy stracisz nogę",
                Use = UseRod,
                Model = r => RodModel.CreateLoose(r, 0).Root.gameObject,
            };
            // Placeholders: wired up when the toy gun / phone branches are merged (Count / Give / Take on their state).
            yield return new ItemDef
            {
                Item = HotbarItem.ToyGun, Name = "Pistolecik z glutem", Users = ItemUsers.Hider,
                DropSfx = GameSfx.RcPlace, Glow = new Color(0.5f, 1f, 0.4f, 0.45f),
                Model = r => Placeholder(r, new Vector3(0.2f, 0.12f, 0.05f), new Color(0.35f, 0.85f, 0.3f)),
            };
            yield return new ItemDef
            {
                Item = HotbarItem.Phone, Name = "Telefon", Users = ItemUsers.Hider,
                DropSfx = GameSfx.RcPlace, Glow = new Color(1f, 0.5f, 0.8f, 0.45f),
                Model = r => Placeholder(r, new Vector3(0.075f, 0.15f, 0.012f), new Color(1f, 0.55f, 0.78f)),
            };
        }

        private static PromptKind Prompt => LocalPlayerController.Instance != null ? LocalPlayerController.Instance.Prompt : PromptKind.None;

        private static string MedkitHint(NetPlayer p)
        {
            if (p.Life.Value == LifeState.Downed) return "LPM przytrzymaj - podnieś się";
            if (Prompt == PromptKind.Heal) return "LPM przytrzymaj - ulecz rannego";
            return p.Wounded.Value || p.Limping.Value ? "LPM przytrzymaj - ulecz się" : "lecz rany (LPM / G, H na rannym)";
        }

        private static void UseMedkit(NetPlayer p, bool click, bool hold)
        {
            if (Prompt == PromptKind.Heal && p.Life.Value == LifeState.Alive) HotbarInput.HoldH = hold;
            else if (p.Life.Value == LifeState.Downed || p.Wounded.Value || p.Limping.Value) HotbarInput.HoldG = hold;
            else if (click) Toast("Nie jesteś ranny. Apteczką możesz też uleczyć rannego (spójrz na niego).");
        }

        private static void UseRod(NetPlayer p, bool click, bool hold)
        {
            if (!click || p.Life.Value != LifeState.Alive) return;
            if (p.FreeStump == 0) Toast("Masz obie nogi. Pręt przyda się, jeśli stracisz nogę.");
            else if (p.Motor.IsGrounded) p.RequestRodAttachRpc();
        }

        private static GameObject Prop(Transform root, HeldItem item) => BlockyProps.Create(item, root, 0).gameObject;

        private static GameObject Placeholder(Transform root, Vector3 size, Color color)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(root, false);
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            return b;
        }
    }
}
