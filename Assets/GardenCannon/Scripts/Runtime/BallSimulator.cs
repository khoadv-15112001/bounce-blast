using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    public enum BallHit { None, Wall, Item, PassThrough }

    public class Ball
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public Transform View;
        public bool Dead;
        public float Age;
        public float SpeedScale = 1f;
        /// <summary>Items broken by this ball (raises the break sound pitch).</summary>
        public int Combo;
        public readonly HashSet<BoardItem> Inside = new HashSet<BoardItem>();
    }

    /// <summary>
    /// Deterministic ball physics ported from the web prototype (index.html):
    /// sub-stepped circle vs walls / items, mirror reflection, anti-horizontal-loop clamp.
    /// The same code drives the aim prediction (ghost = true, no side effects).
    /// </summary>
    [Serializable]
    public class BallSimulator
    {
        [Tooltip("Ball radius in cells.")]
        public float radiusCells = 0.22f;
        [Tooltip("Ball speed in cells per second.")]
        public float speedCells = 17f;
        [Tooltip("Minimum share of the speed kept on the Y axis (prevents endless horizontal bouncing).")]
        [Range(0f, 0.5f)] public float minVerticalRatio = 0.18f;
        [Tooltip("Max distance per sub-step, as a fraction of the radius.")]
        public float subStepRadiusFraction = 0.5f;

        public Board Board;
        public float Radius => radiusCells * (Board != null ? Board.CellSize : 1f);
        public float Speed => speedCells * (Board != null ? Board.CellSize : 1f);

        /// <summary>Raised for real (non-ghost) contacts.</summary>
        public Action<Ball, BoardItem, BallHit> OnContact;

        static readonly List<BoardItem> _tmpInside = new List<BoardItem>();

        public void Advance(Ball b, float dt, bool ghost)
        {
            float r = Radius;
            int sub = Mathf.Max(1, Mathf.CeilToInt(b.Vel.magnitude * dt / Mathf.Max(0.001f, r * subStepRadiusFraction)));
            float h = dt / sub;
            for (int i = 0; i < sub && !b.Dead; i++)
            {
                var hit = Step(b, h, ghost, out var item);
                if (hit != BallHit.None && !ghost) OnContact?.Invoke(b, item, hit);
            }
        }

        public BallHit Step(Ball b, float dt, bool ghost, out BoardItem hitItem)
        {
            hitItem = null;
            float r = Radius;
            b.Pos += b.Vel * dt;
            var result = BallHit.None;

            // Lawn walls. The side walls continue down to the cannon so balls never leave the screen.
            if (b.Pos.x < Board.Left + r) { b.Pos.x = Board.Left + r; b.Vel.x = Mathf.Abs(b.Vel.x); result = BallHit.Wall; }
            if (b.Pos.x > Board.Right - r) { b.Pos.x = Board.Right - r; b.Vel.x = -Mathf.Abs(b.Vel.x); result = BallHit.Wall; }
            if (b.Pos.y > Board.Top - r) { b.Pos.y = Board.Top - r; b.Vel.y = -Mathf.Abs(b.Vel.y); result = BallHit.Wall; }

            // Items: pass-through ones are triggered on entry, the first solid contact is resolved.
            _tmpInside.Clear();
            var items = Board.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.Alive) continue;
                if (!it.Collide(b.Pos, r, out var n, out var pen)) continue;

                if (it.Kind == ItemKind.PassThrough)
                {
                    _tmpInside.Add(it);
                    if (!b.Inside.Contains(it))
                    {
                        hitItem = it;
                        result = BallHit.PassThrough;
                        if (!ghost) OnContact?.Invoke(b, it, BallHit.PassThrough);
                    }
                    continue;
                }

                b.Pos += n * pen;
                float dot = Vector2.Dot(b.Vel, n);
                if (dot < 0f) b.Vel -= 2f * dot * n;
                if (it.Kind == ItemKind.Spinner) b.Vel += it.SurfaceVelocity(b.Pos) * 0.6f;
                hitItem = it;
                result = BallHit.Item;
                break;
            }
            b.Inside.Clear();
            foreach (var it in _tmpInside) b.Inside.Add(it);
            if (result == BallHit.PassThrough) result = BallHit.None; // already reported

            // Keep speed constant and avoid near-horizontal loops.
            float speed = Speed * b.SpeedScale;
            float minVy = speed * minVerticalRatio;
            if (Mathf.Abs(b.Vel.y) < minVy) b.Vel.y = (b.Vel.y >= 0f ? 1f : -1f) * minVy;
            b.Vel = b.Vel.normalized * speed;

            if (b.Vel.y < 0f && b.Pos.y < Board.WaterLine - r) b.Dead = true;
            return result;
        }

        /// <summary>Predict the path for the aim guide.</summary>
        public void Predict(Vector2 start, Vector2 dir, int maxBounces, float maxLength, float dotSpacing,
            List<Vector2> dots, List<Vector2> bounces)
        {
            dots.Clear();
            bounces.Clear();
            var g = new Ball { Pos = start, Vel = dir.normalized * Speed };
            float stepLen = Radius * 0.5f;
            float dt = stepLen / Speed;
            float travelled = 0f, nextDot = dotSpacing * 0.6f;
            Vector2 lastHit = new Vector2(float.MaxValue, float.MaxValue);
            for (int i = 0; i < 4000; i++)
            {
                var hit = Step(g, dt, true, out _);
                travelled += stepLen;
                if (hit == BallHit.Wall || hit == BallHit.Item)
                {
                    if ((g.Pos - lastHit).sqrMagnitude > stepLen * stepLen * 4f)
                    {
                        lastHit = g.Pos;
                        if (bounces.Count >= maxBounces) break;
                        bounces.Add(g.Pos);
                    }
                }
                if (travelled >= nextDot) { dots.Add(g.Pos); nextDot += dotSpacing; }
                if (travelled > maxLength || g.Dead) break;
            }
        }
    }
}
