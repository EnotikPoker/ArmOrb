using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmOrb
{
    [Serializable] public sealed class MiniGameData
    {
        public uint random;
        public bool initialized;
        public int drawingTarget;
        public int[] tiles = new int[16];
        public float clock, rhythmTarget = 0.5f, drawingCooldown;
        public bool rhythmTapped;
    }
    public interface IMiniGame
    {
        MiniGameKind Kind { get; }
        void Tick(float seconds);
    }
    public sealed class MiniGameModel : IMiniGame
    {
        public MiniGameKind Kind { get; }
        public MiniGameData Data { get; }
        public string Feedback { get; private set; } = "";
        public static readonly string[] DrawingNames = { "Линия", "Угол", "Зигзаг", "Треугольник", "Квадрат", "Круг" };
        public float Cursor => Data.clock / 1.2f;
        public MiniGameModel(MiniGameKind kind, MiniGameData data)
        {
            Kind = kind; Data = data;
            if (!Data.initialized)
            {
                Data.initialized = true; Data.drawingTarget = MatchRandom.Range(ref Data.random, 6);
                for (int i = 0; i < 16; i++) Data.tiles[i] = MatchRandom.Range(ref Data.random, 4);
                EnsureMove(); NextRhythm();
            }
        }
        public static bool Valid(MiniGameData data)
        {
            if (data.tiles == null || data.tiles.Length != 16 || data.drawingTarget < 0 || data.drawingTarget >= 6 ||
                !float.IsFinite(data.clock) || data.clock < 0 || data.clock > 1.21f || !float.IsFinite(data.rhythmTarget) || data.rhythmTarget < 0.2f || data.rhythmTarget > 0.8f ||
                !float.IsFinite(data.drawingCooldown) || data.drawingCooldown < 0 || data.drawingCooldown > 0.3f) return false;
            foreach (int tile in data.tiles) if (tile < 0 || tile > 3) return false; return true;
        }
        public void Tick(float seconds)
        {
            Data.drawingCooldown = Mathf.Max(0, Data.drawingCooldown - seconds);
            if (Kind != MiniGameKind.Rhythm) return;
            Data.clock += seconds;
            while (Data.clock >= 1.2f) { Data.clock -= 1.2f; NextRhythm(); }
        }
        void NextRhythm() { Data.rhythmTarget = 0.25f + 0.5f * MatchRandom.Unit(ref Data.random); Data.rhythmTapped = false; }
        public int TapRhythm()
        {
            if (Data.rhythmTapped) return 0;
            Data.rhythmTapped = true;
            float distance = Mathf.Abs(Cursor - Data.rhythmTarget);
            int reward = distance <= 0.045f ? 3 : distance <= 0.12f ? 2 : 0;
            Feedback = reward == 3 ? "Точно · +3" : reward == 2 ? "Попадание · +2" : "Мимо";
            return reward;
        }
        public void SkipDrawing() { NextDrawing(); Feedback = ""; }
        void NextDrawing() { Data.drawingTarget = (Data.drawingTarget + 1 + MatchRandom.Range(ref Data.random, 5)) % 6; }
        public int SubmitDrawing(IReadOnlyList<Vector2> points, float duration)
        {
            if (Data.drawingCooldown > 0) return 0;
            float score = points.Count >= 2 && duration >= 0.12f ? DrawingDistance(points, Data.drawingTarget) : float.PositiveInfinity;
            int reward = score <= 0.10f ? 5 : score <= 0.20f ? 3 : 0;
            Feedback = reward == 5 ? "Точно · +5" : reward == 3 ? "Знак принят · +3" : "Не похоже — попробуй ещё";
            if (reward > 0) NextDrawing();
            Data.drawingCooldown = 0.18f; return reward;
        }
        public static List<Vector2> Template(int target)
        {
            var p = new List<Vector2>();
            switch (target)
            {
                case 0: p.AddRange(new[] { new Vector2(.15f,.5f), new Vector2(.85f,.5f) }); break;
                case 1: p.AddRange(new[] { new Vector2(.15f,.2f), new Vector2(.5f,.8f), new Vector2(.85f,.2f) }); break;
                case 2: p.AddRange(new[] { new Vector2(.2f,.2f), new Vector2(.8f,.2f), new Vector2(.2f,.8f), new Vector2(.8f,.8f) }); break;
                case 3: p.AddRange(new[] { new Vector2(.5f,.15f), new Vector2(.85f,.8f), new Vector2(.15f,.8f), new Vector2(.5f,.15f) }); break;
                case 4: p.AddRange(new[] { new Vector2(.2f,.2f), new Vector2(.8f,.2f), new Vector2(.8f,.8f), new Vector2(.2f,.8f), new Vector2(.2f,.2f) }); break;
                default:
                    for (int i = 0; i <= 48; i++) { float a = i * Mathf.PI * 2 / 48 - Mathf.PI / 2; p.Add(new Vector2(.5f + .33f * Mathf.Cos(a), .5f + .33f * Mathf.Sin(a))); } break;
            }
            return p;
        }
        static Vector2[] Normalize(IReadOnlyList<Vector2> source)
        {
            float length = 0; for (int i = 1; i < source.Count; i++) length += Vector2.Distance(source[i-1], source[i]);
            if (length < .25f) return null;
            var sampled = new Vector2[64]; sampled[0] = source[0];
            int segment = 1; float passed = 0;
            for (int n = 1; n < 64; n++)
            {
                float wanted = length * n / 63;
                while (segment < source.Count - 1 && passed + Vector2.Distance(source[segment-1],source[segment]) < wanted)
                { passed += Vector2.Distance(source[segment-1],source[segment]); segment++; }
                float span = Vector2.Distance(source[segment-1],source[segment]);
                sampled[n] = Vector2.Lerp(source[segment-1],source[segment],span <= 0.00001f ? 0 : (wanted-passed)/span);
            }
            Vector2 min = sampled[0], max = sampled[0];
            foreach (var v in sampled) { min = Vector2.Min(min,v); max = Vector2.Max(max,v); }
            float size = Mathf.Max(max.x-min.x,max.y-min.y); if (size < .15f) return null;
            for (int i = 0; i < sampled.Length; i++) sampled[i] = (sampled[i]-(min+max)*.5f)/size;
            return sampled;
        }
        public static float DrawingDistance(IReadOnlyList<Vector2> points, int target)
        {
            var a = Normalize(points); var b = Normalize(Template(target)); if (a == null || b == null) return float.PositiveInfinity;
            float best = float.PositiveInfinity;
            bool closed = target >= 3;
            if (closed && Vector2.Distance(points[0],points[points.Count-1]) > .20f) return best;
            for (int reverse = 0; reverse < 2; reverse++)
            for (int shift = 0; shift < (closed ? 63 : 1); shift++)
            for (int turn = -1; turn <= 1; turn++)
            {
                float angle = turn * 15 * Mathf.Deg2Rad, sum = 0;
                int count = closed ? 63 : 64;
                for (int i = 0; i < count; i++)
                {
                    int index = closed ? (shift + (reverse == 0 ? i : count-i)) % count : reverse == 0 ? i : 63-i;
                    var v = a[index]; v = new Vector2(v.x*Mathf.Cos(angle)-v.y*Mathf.Sin(angle),v.x*Mathf.Sin(angle)+v.y*Mathf.Cos(angle));
                    sum += Vector2.Distance(v,b[i]);
                }
                best = Mathf.Min(best, sum/count);
            }
            return best;
        }
        public bool CanAppend(IReadOnlyList<int> chain, int tile)
        {
            if (tile < 0 || tile >= 16) return false;
            if (chain.Count == 0) return true;
            foreach (int id in chain) if (id == tile) return false;
            int last = chain[chain.Count-1];
            return Data.tiles[last] == Data.tiles[tile] && Mathf.Abs(last/4-tile/4)+Mathf.Abs(last%4-tile%4) == 1;
        }
        public int SubmitChain(IReadOnlyList<int> chain)
        {
            var verified = new List<int>(); foreach (int tile in chain) { if (!CanAppend(verified,tile)) return 0; verified.Add(tile); }
            if (chain.Count < 3) { Feedback = "Соедини хотя бы три"; return 0; }
            foreach (int tile in chain) Data.tiles[tile] = -1;
            for (int col = 0; col < 4; col++)
            {
                int write = 3;
                for (int row = 3; row >= 0; row--) if (Data.tiles[row*4+col] >= 0) Data.tiles[write--*4+col] = Data.tiles[row*4+col];
                while (write >= 0) Data.tiles[write--*4+col] = MatchRandom.Range(ref Data.random,4);
            }
            EnsureMove(); int reward = chain.Count-1; Feedback = $"Цепочка {chain.Count} · +{reward}"; return reward;
        }
        public bool HasMove()
        {
            for (int i = 0; i < 16; i++)
            {
                int neighbours = 0;
                for (int j = 0; j < 16; j++) if (Mathf.Abs(i/4-j/4)+Mathf.Abs(i%4-j%4) == 1 && Data.tiles[i] == Data.tiles[j]) neighbours++;
                if (neighbours >= 2) return true;
            }
            return false;
        }
        void EnsureMove()
        {
            if (HasMove()) return;
            // A guaranteed fresh chain; never awards essence or triggers an automatic cascade.
            for (int i = 0; i < 16; i++) Data.tiles[i] = MatchRandom.Range(ref Data.random,4);
            Data.tiles[0] = Data.tiles[1] = Data.tiles[2] = MatchRandom.Range(ref Data.random,4);
        }
    }
}
