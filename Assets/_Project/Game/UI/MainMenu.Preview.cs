using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Menu character: stands in a 3/4 pose with a slow sway and shows a weapon. The weapon is picked in the
    /// bottom-right corner (<see cref="MenuWeaponPicker"/>): "?" = a random one each time (knife / Glock / chainsaw,
    /// put away in between), a fixed one = only that weapon, kept in hand, its move repeated.
    /// </summary>
    public partial class MainMenu
    {
        public enum MenuWeapon { Random = 0, Knife = 1, Chainsaw = 2, Glock = 3 }

        private const string WeaponKey = "hns.menuWeapon";
        private float _previewYaw;
        private AudioSource _menuLoop;
        private Coroutine _cycle;

        public static MenuWeapon SavedWeapon
        {
            get => (MenuWeapon)Mathf.Clamp(Prefs.GetInt(WeaponKey, 0), 0, 3);
            set { Prefs.SetInt(WeaponKey, (int)value); Prefs.Save(); }
        }

        private void BuildPreview()
        {
            if (previewRoot == null) return;
            _preview = BlockyCharacterFactory.Create(previewRoot, LocalProfile.Skin, LocalProfile.Nick);
            _preview.SetFirstPersonMode(false);
            if (!string.IsNullOrEmpty(LocalProfile.McNick)) FetchMcSkin(LocalProfile.McNick);
            _previewYaw = previewRoot.eulerAngles.y;
            SetWeapon(SavedWeapon, false);
        }

        /// <summary>Switches the weapon shown by the menu character (from the picker).</summary>
        public void SetWeapon(MenuWeapon w, bool save = true)
        {
            if (save) SavedWeapon = w;
            if (_preview == null) return;
            if (_cycle != null) StopCoroutine(_cycle);
            StopLoop(0.15f);
            var a = _preview.Actions;
            if (a != null)
            {
                a.SetRevving(false);
                a.SetHeldItem(HeldItem.None);
            }
            Debug.Log($"[MainMenu] menu weapon = {w}");
            _cycle = StartCoroutine(w == MenuWeapon.Random ? RandomCycle() : FixedCycle(w));
        }

        private void StopLoop(float fade)
        {
            if (_menuLoop != null) GameAudio.StopLoop(_menuLoop, fade);
            _menuLoop = null;
        }

        private void Sfx(GameSfx id, float v = 0.8f) => GameAudio.PlayAt(id, previewRoot.position + Vector3.up * 1.1f, v);

        /// <summary>"?": a random weapon each time (never the same twice in a row), put away in between.</summary>
        private IEnumerator RandomCycle()
        {
            var last = MenuWeapon.Random;
            yield return new WaitForSeconds(2.5f);
            while (true)
            {
                MenuWeapon w;
                do w = (MenuWeapon)Random.Range(1, 4); while (w == last);
                last = w;
                yield return Show(w);
                _preview.Actions.SetHeldItem(HeldItem.None);
                yield return new WaitForSeconds(4f);
            }
        }

        /// <summary>One weapon: drawn once and kept; its move repeats every few seconds.</summary>
        private IEnumerator FixedCycle(MenuWeapon w)
        {
            var a = _preview.Actions;
            yield return new WaitForSeconds(0.35f);
            switch (w)
            {
                case MenuWeapon.Knife:
                    a.SetHeldItem(HeldItem.Knife);
                    Sfx(GameSfx.KnifeDraw);
                    while (true)
                    {
                        yield return new WaitForSeconds(3.2f);
                        Sfx(GameSfx.KnifeSwing, 0.5f);
                        yield return new WaitForSeconds(0.5f);
                        Sfx(GameSfx.KnifeSwing, 0.4f);
                    }
                case MenuWeapon.Glock:
                    a.SetHeldItem(HeldItem.Glock);
                    Sfx(GameSfx.GlockEquip);
                    while (true)
                    {
                        yield return new WaitForSeconds(3.6f);
                        a.PlayReload();
                        Sfx(GameSfx.GlockReload);
                        yield return new WaitForSeconds(2.4f);
                    }
                default:
                    a.SetHeldItem(HeldItem.Chainsaw);
                    Sfx(GameSfx.ChainsawStart, 0.7f);
                    _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, previewRoot, 0.45f);
                    while (true)
                    {
                        yield return new WaitForSeconds(3.4f);
                        a.SetRevving(true);
                        StopLoop(0.1f);
                        _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawRev, previewRoot, 0.55f);
                        yield return new WaitForSeconds(1.4f);
                        a.SetRevving(false);
                        StopLoop(0.3f);
                        _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, previewRoot, 0.45f);
                    }
            }
        }

        /// <summary>The old show-off of one weapon (random mode): draw, move, put away.</summary>
        private IEnumerator Show(MenuWeapon w)
        {
            var a = _preview.Actions;
            switch (w)
            {
                case MenuWeapon.Knife:
                    a.SetHeldItem(HeldItem.Knife);
                    Sfx(GameSfx.KnifeDraw);
                    yield return new WaitForSeconds(0.9f);
                    Sfx(GameSfx.KnifeSwing, 0.5f);
                    yield return new WaitForSeconds(0.5f);
                    Sfx(GameSfx.KnifeSwing, 0.4f);
                    yield return new WaitForSeconds(1.4f);
                    break;
                case MenuWeapon.Glock:
                    a.SetHeldItem(HeldItem.Glock);
                    Sfx(GameSfx.GlockEquip);
                    yield return new WaitForSeconds(1.2f);
                    a.PlayReload();
                    Sfx(GameSfx.GlockReload);
                    yield return new WaitForSeconds(2.4f);
                    break;
                default:
                    a.SetHeldItem(HeldItem.Chainsaw);
                    Sfx(GameSfx.ChainsawStart, 0.7f);
                    _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, previewRoot, 0.5f);
                    yield return new WaitForSeconds(1.6f);
                    a.SetRevving(true);
                    StopLoop(0.1f);
                    _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawRev, previewRoot, 0.6f);
                    yield return new WaitForSeconds(1.5f);
                    a.SetRevving(false);
                    StopLoop(0.3f);
                    _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, previewRoot, 0.5f);
                    yield return new WaitForSeconds(1.4f);
                    StopLoop(0.4f);
                    break;
            }
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.SetSkin(LocalProfile.Skin, LocalProfile.Nick);
        }
    }
}
