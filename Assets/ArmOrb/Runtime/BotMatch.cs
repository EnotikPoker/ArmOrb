using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    public enum GameMode { Bets, Test, Bots }
    public enum MiniGameKind { Drawing, Tiles, Rhythm }
    public enum BotDifficulty { Easy, Normal, Hard }
    public enum BotPhase { Combat, Shop, Finished }

    // Independent reproducible random streams; no UnityEngine.Random or worker asset access.
    public static class MatchRandom
    {
        public static uint Next(ref uint state) { if (state == 0) state = 0x9e3779b9; state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public static int Range(ref uint state, int max) => (int)(Next(ref state) % (uint)max);
        public static float Unit(ref uint state) => (Next(ref state) >> 8) / 16777216f;
    }
    [Serializable] public sealed class UpgradeCommand
    { public int tick, actor, target; public UpgradeKind kind; }
    [Serializable] public sealed class ShopCard
    { public UpgradeKind kind; public bool sold; }
    [Serializable] public sealed class BotMatchSave
    {
        public int version = 1;
        public string rules = "0.6";
        public FightDefinition fight;
        public MiniGameKind miniGame;
        public BotDifficulty difficulty;
        public int ticks, visit, outcome;
        public BotPhase phase;
        public uint random;
        public double[] essence;
        public MiniGameData puzzle;
        public ShopCard[] cards = Array.Empty<ShopCard>();
        public List<UpgradeCommand> commands = new List<UpgradeCommand>();
        public string purchases = "";
    }
    public sealed class BotMatchController
    {
        public BotMatchSave Data { get; }
        public ArenaSimulation Simulation { get; }
        public MiniGameModel MiniGame { get; }
        public bool Active => Data.phase == BotPhase.Combat;
        public int Balance => (int)Math.Floor(Data.essence[0] + 1e-7);
        public int NextShop => (Data.visit + 1) * 20;
        public static readonly string[] MiniNames = { "Знаки", "Плитки", "Ритм" };
        public static readonly string[] DifficultyNames = { "Легко", "Обычно", "Сложно" };
        public static int Cost(UpgradeKind kind) => kind switch { UpgradeKind.Heal => 25, UpgradeKind.Armor => 35, UpgradeKind.Weapon => 40, _ => 30 };
        public static string Title(UpgradeKind kind) => kind switch { UpgradeKind.Heal => "Лечение", UpgradeKind.Armor => "Защита", UpgradeKind.Weapon => "Оружие", UpgradeKind.Vulnerability => "Уязвимость", _ => "Отравление" };
        public static string Description(UpgradeKind kind, WeaponKind weapon) => kind switch
        {
            UpgradeKind.Heal => "+60 HP, до начального максимума",
            UpgradeKind.Armor => "−10% боевого урона · до 3 покупок",
            UpgradeKind.Vulnerability => "+20% урона по выбранному врагу · 20 с",
            UpgradeKind.Poison => "Выбранный враг теряет 2 HP/с · 20 с",
            _ => weapon switch
            {
                WeaponKind.Sword => "+2 к урону", WeaponKind.Spear => "+0,75r к длине",
                WeaponKind.Blade => "+20% скорости вращения", WeaponKind.Scythe => "+1 к яду за удар",
                WeaponKind.Shield => "+0,5r к ширине", WeaponKind.Unarmed => "+10% к заработанному разгону",
                WeaponKind.Bow or WeaponKind.Crossbow => "+2 стрелы следующим очередям / залпам",
                WeaponKind.Staff => "+0,5 к радиусу волн", _ => "+2 отскока следующим сюрикенам"
            }
        };
        public static FightDefinition CreateOffer(int weapon, int bots, int seed)
        {
            bots = Mathf.Clamp(bots, 1, 4); var random = new System.Random(seed);
            var f = new FightDefinition { weapons = new int[bots + 1], health = new int[bots + 1], arena = (ArenaSize)bots, seed = seed };
            for (int i = 0; i <= bots; i++) { f.weapons[i] = i == 0 ? Mathf.Clamp(weapon, 0, 9) : random.Next(10); f.health[i] = 300; }
            return f;
        }
        public BotMatchController(SimulationFactory factory, FightDefinition fight, MiniGameKind mini, BotDifficulty difficulty)
            : this(factory, new BotMatchSave { fight = fight.Copy(), miniGame = mini, difficulty = difficulty,
                random = (uint)fight.seed ^ 0xa341316c, essence = new double[fight.Count], puzzle = new MiniGameData { random = (uint)fight.seed ^ 0xc8013ea4 } }, false) { }
        public BotMatchController(SimulationFactory factory, BotMatchSave save) : this(factory, save, true) { }
        BotMatchController(SimulationFactory factory, BotMatchSave save, bool restore)
        {
            Data = save; Simulation = factory.Create(save.fight);
            if (restore)
            {
                int command = 0;
                while (true)
                {
                    while (command < save.commands.Count && save.commands[command].tick == Simulation.TickCount)
                    {
                        var c = save.commands[command++];
                        if (!Simulation.ApplyUpgrade(c.actor, c.kind, c.target)) throw new InvalidDataException("Invalid upgrade journal");
                    }
                    if (Simulation.TickCount == save.ticks) break;
                    if (Simulation.Result != MatchResult.InProgress) throw new InvalidDataException("Invalid battle tick");
                    Simulation.Step(1f / factory.Frequency);
                }
                if (command != save.commands.Count) throw new InvalidDataException("Invalid upgrade time");
            }
            MiniGame = new MiniGameModel(save.miniGame, save.puzzle);
        }
        public void Tick(float dt)
        {
            if (!Active) return;
            Simulation.Step(dt); Data.ticks = Simulation.TickCount;
            MiniGame.Tick(dt);
            double income = Data.difficulty switch { BotDifficulty.Easy => 1.4, BotDifficulty.Hard => 2.6, _ => 2.0 };
            for (int i = 1; i < Data.essence.Length; i++) if (!Simulation.Orbs[i].Health.IsDead) Data.essence[i] += income * dt;
            if (Simulation.Result != MatchResult.InProgress || Simulation.Orbs[0].Health.IsDead)
            {
                Data.outcome = Simulation.Result == MatchResult.Draw ? 2 : Simulation.WinnerIndex == 0 ? 1 : 0;
                Data.phase = BotPhase.Finished; return;
            }
            if (Data.visit < 4 && Simulation.Elapsed >= NextShop)
            { Data.visit++; Data.phase = BotPhase.Shop; BuildShop(); }
        }
        public void Earn(int reward) { if (Active && reward > 0 && reward <= 20) Data.essence[0] += reward; }
        void BuildShop()
        {
            var available = new List<UpgradeKind>();
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) if (Simulation.CanUpgrade(0, kind, (int)kind >= (int)UpgradeKind.Vulnerability ? LivingTarget(0, false) : -1)) available.Add(kind);
            var result = new List<ShopCard>();
            foreach (var kind in available) if ((int)kind <= (int)UpgradeKind.Weapon) result.Add(new ShopCard { kind = kind });
            var sabotage = available.FindAll(k => (int)k >= (int)UpgradeKind.Vulnerability);
            if (sabotage.Count > 0) result.Add(new ShopCard { kind = sabotage[MatchRandom.Range(ref Data.random, sabotage.Count)] });
            while (result.Count < 5 && available.Count > 0) result.Add(new ShopCard { kind = available[MatchRandom.Range(ref Data.random, available.Count)] });
            for (int i = result.Count - 1; i > 0; i--) { int j = MatchRandom.Range(ref Data.random, i + 1); var temp = result[i]; result[i] = result[j]; result[j] = temp; }
            Data.cards = result.ToArray(); Data.purchases = "";
        }
        public bool Buy(int slot, int target = -1)
        {
            if (Data.phase != BotPhase.Shop || slot < 0 || slot >= Data.cards.Length || Data.cards[slot].sold) return false;
            var card = Data.cards[slot];
            if (!Purchase(0, card.kind, target)) return false;
            card.sold = true; return true;
        }
        bool Purchase(int actor, UpgradeKind kind, int target)
        {
            int cost = Cost(kind);
            if (Data.essence[actor] + 1e-7 < cost || !Simulation.ApplyUpgrade(actor, kind, target)) return false;
            Data.essence[actor] -= cost;
            Data.commands.Add(new UpgradeCommand { tick = Simulation.TickCount, actor = actor, target = target, kind = kind });
            Data.purchases += $"#{actor + 1}: {Title(kind)}" + (target >= 0 ? $" → #{target + 1}" : "") + "\n";
            return true;
        }
        int LivingTarget(int actor, bool strongest)
        {
            var ids = new List<int>(); for (int i = 0; i < Simulation.Orbs.Length; i++) if (i != actor && !Simulation.Orbs[i].Health.IsDead) ids.Add(i);
            if (ids.Count == 0) return -1;
            if (!strongest) return ids[MatchRandom.Range(ref Data.random, ids.Count)];
            int best = ids[0]; foreach (int id in ids) if (Simulation.Orbs[id].Health.Current > Simulation.Orbs[best].Health.Current) best = id; return best;
        }
        public void Continue()
        {
            if (Data.phase != BotPhase.Shop) return;
            for (int id = 1; id < Simulation.Orbs.Length; id++)
            {
                if (Simulation.Orbs[id].Health.IsDead) continue;
                // Each bot has its own finite five-card shop, with the same prices and eligibility rules.
                var stock = new List<UpgradeKind>();
                for (int slot = 0; slot < 5; slot++) stock.Add((UpgradeKind)MatchRandom.Range(ref Data.random, 5));
                if (Data.difficulty != BotDifficulty.Easy) { stock[0] = UpgradeKind.Heal; stock[1] = UpgradeKind.Weapon; stock[2] = UpgradeKind.Armor; }
                for (int buy = 0; buy < 5; buy++)
                {
                    int target = LivingTarget(id, Data.difficulty == BotDifficulty.Hard);
                    var valid = stock.FindAll(k => Cost(k) <= Data.essence[id] + 1e-7 && Simulation.CanUpgrade(id, k, (int)k >= (int)UpgradeKind.Vulnerability ? target : -1));
                    if (valid.Count == 0) break;
                    UpgradeKind choice = valid[MatchRandom.Range(ref Data.random, valid.Count)];
                    if (Data.difficulty != BotDifficulty.Easy)
                    {
                        if (Simulation.Orbs[id].Health.Current < 0.4f * Simulation.Orbs[id].Health.Maximum && valid.Contains(UpgradeKind.Heal)) choice = UpgradeKind.Heal;
                        else if (valid.Contains(UpgradeKind.Weapon)) choice = UpgradeKind.Weapon;
                        else if (valid.Contains(UpgradeKind.Armor)) choice = UpgradeKind.Armor;
                    }
                    Purchase(id, choice, (int)choice >= (int)UpgradeKind.Vulnerability ? target : -1); stock.Remove(choice);
                }
            }
            Data.phase = BotPhase.Combat; Data.cards = Array.Empty<ShopCard>();
        }
    }
    public sealed class BotMatchStore
    {
        readonly string path;
        public BotMatchSave Pending { get; private set; }
        public string Error { get; private set; }
        public BotMatchStore(string directory)
        {
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "bot-match.json");
            Pending = Read(path) ?? Read(path + ".bak");
            if (Pending == null && (File.Exists(path) || File.Exists(path + ".bak"))) Error = "Сохранённая битва с ботами несовместима или повреждена. Можно начать новую.";
        }
        static BotMatchSave Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var d = JsonUtility.FromJson<BotMatchSave>(File.ReadAllText(file));
                if (d == null || d.version != 1 || d.rules != "0.6" || d.fight == null || !d.fight.IsValid() || d.ticks < 0 || d.ticks > 20000 ||
                    (int)d.phase < 0 || (int)d.phase >= (int)BotPhase.Finished || (int)d.miniGame < 0 || (int)d.miniGame > 2 || (int)d.difficulty < 0 || (int)d.difficulty > 2 ||
                    d.visit < 0 || d.visit > 4 || d.essence == null || d.essence.Length != d.fight.Count || d.commands == null || d.commands.Count > 200 || d.puzzle == null || d.cards == null) return null;
                foreach (double e in d.essence) if (double.IsNaN(e) || double.IsInfinity(e) || e < -0.001 || e > 100000) return null;
                int previous = 0; foreach (var c in d.commands) { if (c == null || c.tick < previous || c.tick > d.ticks || c.actor < 0 || c.actor >= d.fight.Count || (int)c.kind < 0 || (int)c.kind > 4) return null; previous = c.tick; }
                return MiniGameModel.Valid(d.puzzle) ? d : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }
        public void Save(BotMatchSave data)
        {
            if (data.phase == BotPhase.Finished) { Clear(); return; }
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
            Pending = JsonUtility.FromJson<BotMatchSave>(JsonUtility.ToJson(data)); Error = null;
        }
        public void Clear() { foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); Pending = null; Error = null; }
    }
}
