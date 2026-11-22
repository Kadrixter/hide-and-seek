using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>What one frame of the arcade screen shows (live game from <see cref="ArcadeState"/> or the attract demo).</summary>
    public sealed class SnakeView
    {
        public List<Vector2Int> Body = new();
        public Vector2Int Food;
        public bool HasBonus;
        public Vector2Int Bonus;
        public float BonusLeft;
        public int Score, Best, Level = 1, Dir = 1;
        public byte State = ArcadeState.Idle;
        public float StateAge;
        public bool NewRecord;
        public List<(Vector2Int cell, string text, float t)> Popups;
    }

    /// <summary>
    /// Retro pixel renderer of the SNAKE cabinet (256x192, point filtered): framed playfield with a checker floor,
    /// HUD (score, level, record, bonus timer), start screen "NACIŚNIJ STRZAŁKĘ", GAME OVER with score and record,
    /// attract mode with the title, scanlines.
    /// </summary>
    public sealed partial class SnakeScreen
    {
        public const int TexW = 256, TexH = 192;
        private const int FieldW = SnakeRules.W * Cell, FieldH = SnakeRules.H * Cell;
        private const int Hud = GridY + FieldH + 7; // first HUD row

        private readonly SnakeCanvas _c = new(TexW, TexH);
        private int _lastLevel = 1;
        private float _levelFlashUntil;

        public Color32[] Pixels => _c.Px;

        private static readonly Color32 Bg = new(5, 6, 16, 255);
        private static readonly Color32 CheckA = new(12, 16, 36, 255), CheckB = new(15, 21, 44, 255);
        private static readonly Color32 Yellow = new(255, 222, 70, 255), Cyan = new(130, 210, 255, 255), Label = new(110, 150, 215, 255);
        private static readonly Color32 White = new(240, 244, 255, 255), Red = new(255, 64, 64, 255), Green = new(110, 255, 120, 255);

        public void Draw(SnakeView v, float t)
        {
            _c.Clear(Bg);
            bool idle = v.State == ArcadeState.Idle, over = v.State == ArcadeState.Over, ready = v.State == ArcadeState.Ready;
            if (v.State == ArcadeState.Playing && v.Level > _lastLevel) _levelFlashUntil = t + 1.3f;
            _lastLevel = v.State == ArcadeState.Playing ? v.Level : 1;

            DrawField();
            float dim = idle ? 0.55f : 1f;
            if (!over || (int)(t * 3f) % 2 == 0 || v.StateAge > 1.5f) DrawFood(v.Food, t);
            if (v.HasBonus && !over) DrawBonus(v.Bonus, v.BonusLeft, t);
            DrawSnake(v.Body, v.Dir, over, t, dim);
            if (v.Popups != null)
                foreach (var (cell, text, pt) in v.Popups)
                {
                    float age = t - pt;
                    if (age < 0f || age > 0.9f) continue;
                    var col = SnakeCanvas.Lerp(Yellow, new Color32(120, 80, 10, 255), age / 0.9f);
                    _c.TextC(text, Px(cell.x) + 3, Py(cell.y) + 6 + (int)(age * 14f), 1, col);
                }
            DrawFrame(over, t < _levelFlashUntil && (int)(t * 8f) % 2 == 0);
            DrawHud(v, t);

            if (idle) DrawAttract(v, t);
            else if (ready) DrawReady(t);
            else if (over) DrawOver(v, t);
            else if (t < _levelFlashUntil) _c.TextC("POZIOM " + v.Level + "!", GridX + FieldW / 2, GridY + FieldH - 24, 2, Cyan);

            _c.Scanlines(0.8f);
        }

        // ================================================================ playfield and frame

        private void DrawField()
        {
            for (int cy = 0; cy < SnakeRules.H; cy++)
            for (int cx = 0; cx < SnakeRules.W; cx++)
                _c.Rect(Px(cx), Py(cy), Cell, Cell, ((cx + cy) & 1) == 0 ? CheckA : CheckB);
        }

        private void DrawFrame(bool over, bool flash)
        {
            Color32 outer = over ? new Color32(170, 30, 40, 255) : flash ? new Color32(255, 255, 255, 255) : new Color32(40, 96, 230, 255);
            Color32 mid = over ? new Color32(255, 90, 90, 255) : flash ? Cyan : new Color32(130, 205, 255, 255);
            Color32 inner = over ? new Color32(70, 10, 16, 255) : new Color32(18, 36, 100, 255);
            int x = GridX, y = GridY;
            _c.Outline(x - 4, y - 4, FieldW + 8, FieldH + 8, outer);
            _c.Outline(x - 3, y - 3, FieldW + 6, FieldH + 6, mid);
            _c.Outline(x - 2, y - 2, FieldW + 4, FieldH + 4, outer);
            _c.Outline(x - 1, y - 1, FieldW + 2, FieldH + 2, inner);
            // corner rivets
            foreach (var (cx, cy) in new[] { (x - 4, y - 4), (x + FieldW + 1, y - 4), (x - 4, y + FieldH + 1), (x + FieldW + 1, y + FieldH + 1) })
                _c.Rect(cx, cy, 3, 3, mid);
        }

        // ================================================================ HUD

        private void DrawHud(SnakeView v, float t)
        {
            int left = GridX - 2, right = GridX + FieldW + 2, mid = GridX + FieldW / 2;
            _c.Text("WYNIK", left, TexH - 9, 1, Label);
            _c.Text(v.Score.ToString(), left, Hud, 2, Yellow);
            _c.TextR("REKORD", right, TexH - 9, 1, Label);
            bool beat = v.NewRecord && (int)(t * 4f) % 2 == 0;
            _c.TextR(Mathf.Max(v.Best, v.Score).ToString(), right, Hud, 2, beat ? Yellow : Cyan);
            if (v.State == ArcadeState.Idle)
            {
                _c.TextC("SNAKE", mid, TexH - 9, 1, Green);
                return;
            }
            _c.TextC("POZIOM", mid, TexH - 9, 1, Label);
            _c.TextC(v.Level.ToString(), mid, Hud + 8, 1, White);
            if (v.HasBonus && v.State == ArcadeState.Playing)
            {
                // golden timer bar under the level
                int bw = 56, bx = mid - bw / 2;
                int fill = Mathf.RoundToInt(bw * Mathf.Clamp01(v.BonusLeft / SnakeRules.BonusTime));
                _c.Outline(bx - 1, Hud - 1, bw + 2, 5, new Color32(120, 90, 20, 255));
                _c.Rect(bx, Hud, fill, 3, v.BonusLeft < 2f && (int)(t * 10f) % 2 == 0 ? Red : Yellow);
            }
        }

        // ================================================================ overlays

        private void Panel(int y, int h, Color32 border)
        {
            int w = 200, x = GridX + FieldW / 2 - w / 2;
            _c.Dim(x, y, w, h, 0.25f);
            _c.Outline(x, y, w, h, border);
            _c.Outline(x + 2, y + 2, w - 4, h - 4, SnakeCanvas.Lerp(border, Bg, 0.6f));
        }

        private void DrawReady(float t)
        {
            int mid = GridX + FieldW / 2, top = GridY + FieldH - 8;
            Panel(top - 58, 60, new Color32(90, 220, 110, 255));
            _c.TextC("SNAKE", mid, top - 22, 2, Green);
            if ((int)(t * 2.5f) % 2 == 0) _c.TextC("NACIŚNIJ STRZAŁKĘ", mid, top - 37, 1, White);
            _c.TextC("LUB W A S D", mid, top - 47, 1, Label);
        }

        private void DrawOver(SnakeView v, float t)
        {
            int mid = GridX + FieldW / 2;
            _c.Dim(GridX, GridY, FieldW, FieldH, 0.55f);
            int y0 = GridY + 22;
            Panel(y0, 104, new Color32(255, 80, 80, 255));
            _c.TextC("GAME OVER", mid, y0 + 80, 2, Red);
            _c.TextC("WYNIK  " + v.Score, mid, y0 + 64, 1, White);
            _c.TextC("REKORD " + Mathf.Max(v.Best, v.Score), mid, y0 + 53, 1, Cyan);
            if (v.NewRecord && (int)(t * 4f) % 2 == 0) _c.TextC("NOWY REKORD!", mid, y0 + 38, 1, Yellow);
            if (v.StateAge > 1f && (int)(t * 2.5f) % 2 == 0) _c.TextC("NACIŚNIJ STRZAŁKĘ", mid, y0 + 20, 1, White);
            if (v.StateAge > 1f) _c.TextC("E - WYJŚCIE", mid, y0 + 8, 1, Label);
        }

        private static readonly Color32[] TitleCols =
        {
            new(110, 255, 120, 255), new(255, 222, 70, 255), new(255, 110, 160, 255), new(130, 210, 255, 255), new(255, 160, 60, 255),
        };

        private void DrawAttract(SnakeView v, float t)
        {
            int mid = GridX + FieldW / 2;
            const string title = "SNAKE";
            int scale = 4, x = mid - SnakeCanvas.TextWidth(title, scale) / 2, y = GridY + FieldH - 46;
            for (int i = 0; i < title.Length; i++)
            {
                int bounce = Mathf.RoundToInt(Mathf.Max(0f, Mathf.Sin(t * 4f - i * 0.7f)) * 3f);
                var col = TitleCols[(i + (int)(t * 2f)) % TitleCols.Length];
                _c.Text(title[i].ToString(), x + i * 6 * scale, y + bounce, scale, col);
            }
            if ((int)(t * 2f) % 2 == 0) _c.TextC("PODEJDŹ I WCIŚNIJ E", mid, GridY + 34, 1, White);
            _c.TextC("REKORD " + v.Best, mid, GridY + 20, 1, Cyan);
        }
    }
}
