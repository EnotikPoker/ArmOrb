using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    public enum GameMode { Bets, Test, Bots, Runs, Collection }
    public enum MiniGameKind { Drawing, Tiles, Rhythm }
    public enum BotDifficulty { Easy, Normal, Hard }
    public enum BotPhase { Combat, Choice, Finished }
    public enum RewardMode { Gauge, Timed }

    // Independent reproducible random streams; no UnityEngine.Random or worker asset access.
    public static class MatchRandom
    {
        public static uint Next(ref uint state) { if (state == 0) state = 0x9e3779b9; state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public static int Range(ref uint state, int max) => (int)(Next(ref state) % (uint)max);
        public static float Unit(ref uint state) => (Next(ref state) >> 8) / 16777216f;
    }
    [Serializable] public sealed class UpgradeCommand
    { public int tick, actor, target; public UpgradeKind kind; }
    [Serializable] public sealed class SkillCard
    { public UpgradeKind kind; public int target = -1; }
    [Serializable] public sealed class SkillOffer
    {
        public bool open;
        public int id, deadlineTick, actionTick;
        public SkillCard[] cards = Array.Empty<SkillCard>();
    }
    [Serializable] public sealed class BotMatchSave
    {
        public int version = 2;
        public string rules = "0.7";
        public FightDefinition fight;
        public MiniGameKind miniGame;
        public BotDifficulty difficulty;
        public RewardMode rewardMode;
        public int frequency = 120, ticks, visit, outcome, nextTimedTick, sequence;
        public BotPhase phase;
        public uint random;
        public double[] essence;
        public int[] incomeRemainder;
        public MiniGameData puzzle;
        public SkillOffer[] offers;
        public List<UpgradeCommand> commands = new List<UpgradeCommand>();
        public string notice = "";
        public int noticeTick;

        public static void Migrate(BotMatchSave data)
        {
            if (data.version != 1 || data.rules != "0.6") return;
            data.version = 2; data.rules = "0.7"; data.rewardMode = RewardMode.Timed;
            data.frequency = 120; data.phase = BotPhase.Combat;
            data.nextTimedTick = (data.ticks / (30 * data.frequency) + 1) * 30 * data.frequency;
            data.offers = NewOffers(data.fight.Count); data.puzzle.restRemaining = 0;
            data.incomeRemainder = new int[data.fight.Count];
            for(int i=0;i<data.essence.Length;i++)
            {
                double whole=Math.Floor(data.essence[i]+1e-7);
                data.incomeRemainder[i]=Mathf.Clamp((int)Math.Round((data.essence[i]-whole)*data.frequency*10),0,data.frequency*10-1);
                data.essence[i]=whole;
            }
        }
        public static SkillOffer[] NewOffers(int count)
        {
            var offers = new SkillOffer[count]; for (int i = 0; i < count; i++) offers[i] = new SkillOffer(); return offers;
        }
    }
    public sealed class BotMatchController
    {
        public const int GaugeCost = 60;
        public BotMatchSave Data { get; }
        public ArenaSimulation Simulation { get; }
        public MiniGameModel MiniGame { get; }
        public bool Active => Data.phase != BotPhase.Finished;
        public bool TasksReady => Active && !Data.offers[0].open && MiniGame.Ready;
        public int Balance => (int)Math.Floor(Data.essence[0] + 1e-7);
        public int NextOfferSeconds => Data.nextTimedTick / Data.frequency;
        public float ChoiceRemaining => Mathf.Max(0, (Data.offers[0].deadlineTick - Data.ticks) / (float)Data.frequency);
        public string Notice => Data.ticks - Data.noticeTick < Data.frequency * 3 ? Data.notice : "";
        public static readonly string[] RewardNames = { "По шкале", "Каждые 30 секунд" };
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
        public BotMatchController(SimulationFactory factory, FightDefinition fight, MiniGameKind mini, BotDifficulty difficulty, RewardMode mode = RewardMode.Gauge)
            : this(factory, new BotMatchSave { fight = fight.Copy(), miniGame = mini, difficulty = difficulty, rewardMode = mode,
                frequency = factory.Frequency, nextTimedTick = factory.Frequency * 30, offers = BotMatchSave.NewOffers(fight.Count),
                random = (uint)fight.seed ^ 0xa341316c, essence = new double[fight.Count], incomeRemainder = new int[fight.Count], puzzle = new MiniGameData { random = (uint)fight.seed ^ 0xc8013ea4 } }, false) { }
        public BotMatchController(SimulationFactory factory, BotMatchSave save) : this(factory, save, true) { }
        BotMatchController(SimulationFactory factory, BotMatchSave save, bool restore)
        {
            BotMatchSave.Migrate(save);
            if (save.frequency != factory.Frequency) throw new InvalidDataException("Battle frequency changed");
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
            if (Simulation.Result != MatchResult.InProgress || Simulation.Orbs[0].Health.IsDead)
            {
                Data.outcome = Simulation.Result == MatchResult.Draw ? 2 : Simulation.WinnerIndex == 0 ? 1 : 0;
                Data.phase = BotPhase.Finished;
                foreach (var offer in Data.offers) offer.open = false;
                return;
            }
            if (!Data.offers[0].open) MiniGame.Tick(dt);
            // Integer subunits avoid balance drift over JSON round trips; one tick is 1/frequency seconds.
            int income = Data.difficulty switch { BotDifficulty.Easy => 14, BotDifficulty.Hard => 26, _ => 20 };
            int denominator = Data.frequency * 10;
            for (int i = 1; i < Data.essence.Length; i++) if (!Simulation.Orbs[i].Health.IsDead)
            {
                Data.incomeRemainder[i] += income;
                Data.essence[i] += Data.incomeRemainder[i] / denominator;
                Data.incomeRemainder[i] %= denominator;
            }
            bool timedMoment = Data.ticks >= Data.nextTimedTick;
            if (timedMoment) do { Data.nextTimedTick += 30 * Data.frequency; } while (Data.nextTimedTick <= Data.ticks);
            if (Data.rewardMode == RewardMode.Timed && timedMoment)
            {
                for (int i = 0; i < Data.offers.Length; i++) OpenOffer(i);
            }
            else if (Data.rewardMode == RewardMode.Gauge)
            {
                // The player opens a gauge offer only after a new successful task, never a chain of queued windows.
                for (int i = 1; i < Data.offers.Length; i++) if (Data.essence[i] + 1e-7 >= GaugeCost) OpenOffer(i);
            }
            for (int i = 0; i < Data.offers.Length; i++)
            {
                var offer = Data.offers[i]; if (!offer.open) continue;
                if (Simulation.Orbs[i].Health.IsDead) { CloseOffer(i); continue; }
                if (i == 0 && Data.ticks >= offer.deadlineTick) ResolveAutomatic(i);
                else if (i > 0 && Data.ticks >= offer.actionTick) ResolveAutomatic(i);
            }
        }
        public void Earn(int reward)
        {
            if (!Active || Data.offers[0].open || reward <= 0 || reward > 20) return;
            Data.essence[0] += reward;
            if (Data.rewardMode == RewardMode.Gauge && Balance >= GaugeCost) OpenOffer(0);
        }
        int Price(SkillCard card) => Data.rewardMode == RewardMode.Gauge ? GaugeCost : Cost(card.kind);
        public bool CanChoose(int actor, int slot)
        {
            if (!Active || actor < 0 || actor >= Data.offers.Length) return false;
            var offer = Data.offers[actor];
            return offer.open && slot >= 0 && slot < offer.cards.Length && Data.essence[actor] + 1e-7 >= Price(offer.cards[slot]) &&
                Simulation.CanUpgrade(actor, offer.cards[slot].kind, offer.cards[slot].target);
        }
        public int AutoChoice(int actor = 0)
        { for (int i = 0; i < Data.offers[actor].cards.Length; i++) if (CanChoose(actor, i)) return i; return -1; }
        public bool Choose(int slot)
        {
            if (Data.ticks >= Data.offers[0].deadlineTick) return false;
            return Purchase(0, slot);
        }
        bool Purchase(int actor, int slot)
        {
            if (!CanChoose(actor, slot)) return false;
            var card = Data.offers[actor].cards[slot];
            if (!Simulation.ApplyUpgrade(actor, card.kind, card.target)) return false;
            Data.essence[actor] -= Price(card);
            Data.commands.Add(new UpgradeCommand { tick = Data.ticks, actor = actor, target = card.target, kind = card.kind });
            Data.notice = (actor == 0 ? "Твой шар" : $"#{actor + 1}") + ": " + Title(card.kind) + (card.target >= 0 ? $" → #{card.target + 1}" : "");
            Data.noticeTick = Data.ticks;
            CloseOffer(actor); return true;
        }
        void CloseOffer(int actor)
        {
            Data.offers[actor].open = false;
            if (actor == 0) { Data.phase = BotPhase.Combat; MiniGame.ResumeAfterChoice(); }
        }
        void ResolveAutomatic(int actor)
        {
            int slot = AutoChoice(actor);
            if (actor > 0 && Data.difficulty == BotDifficulty.Easy)
            {
                var valid = new List<int>(); for (int i = 0; i < 3; i++) if (CanChoose(actor, i)) valid.Add(i);
                if (valid.Count > 0) slot = valid[MatchRandom.Range(ref Data.random, valid.Count)];
            }
            else if (actor > 0 && Data.difficulty != BotDifficulty.Easy)
            {
                var priorities = Simulation.Orbs[actor].Health.Current < .4f * Simulation.Orbs[actor].Health.Maximum
                    ? new[] { UpgradeKind.Heal, UpgradeKind.Weapon, UpgradeKind.Armor }
                    : new[] { UpgradeKind.Weapon, UpgradeKind.Armor, UpgradeKind.Heal };
                foreach (var kind in priorities)
                {
                    int preferred = Array.FindIndex(Data.offers[actor].cards, c => c.kind == kind);
                    if (preferred >= 0 && CanChoose(actor, preferred)) { slot = preferred; break; }
                }
            }
            if (slot < 0 || !Purchase(actor, slot)) CloseOffer(actor);
        }
        bool OpenOffer(int actor)
        {
            if (!Active || Data.offers[actor].open || Simulation.Orbs[actor].Health.IsDead) return false;
            var candidates = new List<SkillCard>();
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
            {
                int target = (int)kind >= 3 ? LivingTarget(actor, actor > 0 && Data.difficulty == BotDifficulty.Hard) : -1;
                if (Simulation.CanUpgrade(actor, kind, target)) candidates.Add(new SkillCard { kind = kind, target = target });
            }
            var affordable = candidates.FindAll(c => Data.essence[actor] + 1e-7 >= Price(c));
            if (affordable.Count == 0) return false;
            for (int i = candidates.Count - 1; i > 0; i--) { int j = MatchRandom.Range(ref Data.random, i + 1); var temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp; }
            var cards = new SkillCard[3]; for (int i = 0; i < 3; i++) { var c = candidates[i % candidates.Count]; cards[i] = new SkillCard { kind = c.kind, target = c.target }; }
            if (!Array.Exists(cards, c => Data.essence[actor] + 1e-7 >= Price(c))) cards[0] = affordable[MatchRandom.Range(ref Data.random, affordable.Count)];
            float delay = Data.difficulty switch { BotDifficulty.Easy => 4 + MatchRandom.Unit(ref Data.random) * 3,
                BotDifficulty.Hard => 1 + MatchRandom.Unit(ref Data.random) * 2, _ => 2 + MatchRandom.Unit(ref Data.random) * 3 };
            Data.offers[actor] = new SkillOffer { open = true, id = ++Data.sequence, cards = cards,
                deadlineTick = Data.ticks + 7 * Data.frequency, actionTick = Data.ticks + Mathf.CeilToInt(delay * Data.frequency) };
            if (actor == 0) { Data.phase = BotPhase.Choice; Data.visit++; }
            return true;
        }
        int LivingTarget(int actor, bool strongest)
        {
            var ids = new List<int>(); for (int i = 0; i < Simulation.Orbs.Length; i++) if (i != actor && !Simulation.Orbs[i].Health.IsDead) ids.Add(i);
            if (ids.Count == 0) return -1;
            if (!strongest) return ids[MatchRandom.Range(ref Data.random, ids.Count)];
            int best = ids[0]; foreach (int id in ids) if (Simulation.Orbs[id].Health.Current > Simulation.Orbs[best].Health.Current) best = id; return best;
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
                if (d == null || d.fight == null || !d.fight.IsValid() || d.puzzle == null || d.essence == null || d.essence.Length != d.fight.Count) return null;
                BotMatchSave.Migrate(d);
                if (d.version != 2 || d.rules != "0.7" || d.frequency < 30 || d.frequency > 240 || d.ticks < 0 || d.ticks > 40000 ||
                    (int)d.phase < 0 || (int)d.phase >= (int)BotPhase.Finished || (int)d.rewardMode < 0 || (int)d.rewardMode > 1 ||
                    (int)d.miniGame < 0 || (int)d.miniGame > 2 || (int)d.difficulty < 0 || (int)d.difficulty > 2 ||
                    d.nextTimedTick <= d.ticks || d.nextTimedTick > d.ticks + d.frequency * 30 ||
                    d.essence == null || d.essence.Length != d.fight.Count || d.incomeRemainder == null || d.incomeRemainder.Length != d.fight.Count || d.commands == null || d.commands.Count > 200 || d.offers == null || d.offers.Length != d.fight.Count) return null;
                foreach (double e in d.essence) if (double.IsNaN(e) || double.IsInfinity(e) || e < -0.001 || e > 100000) return null;
                foreach(int fraction in d.incomeRemainder)if(fraction<0||fraction>=d.frequency*10)return null;
                int previous = 0; foreach (var c in d.commands) { if (c == null || c.tick < previous || c.tick > d.ticks || c.actor < 0 || c.actor >= d.fight.Count || (int)c.kind < 0 || (int)c.kind > 4) return null; previous = c.tick; }
                foreach (var offer in d.offers)
                {
                    if (offer == null || offer.cards == null || (offer.open && (offer.cards.Length != 3 || offer.deadlineTick < d.ticks || offer.deadlineTick > d.ticks + d.frequency * 7))) return null;
                    foreach (var card in offer.cards) if (card == null || (int)card.kind < 0 || (int)card.kind > 4 || card.target < -1 || card.target >= d.fight.Count) return null;
                }
                if ((d.phase == BotPhase.Choice) != d.offers[0].open) return null;
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
