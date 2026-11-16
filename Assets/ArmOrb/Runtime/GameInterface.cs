using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed class GameInterface
    {
        readonly ArmOrbGame game;
        readonly VisualElement root;
        VisualElement app, overlay;
        Button sound;
        Label timer;
        readonly Label[] hp = new Label[2], stats = new Label[2], poison = new Label[2];
        readonly VisualElement[] healthBars = new VisualElement[2];
        Rect previousSafe;
        Vector2Int previousSize;

        public GameInterface(ArmOrbGame game)
        {
            this.game = game; root = game.document.rootVisualElement;
            root.styleSheets.Add(game.styleSheet); root.style.unityFont = game.uiFont;
        }
        VisualElement Element(VisualElement parent, string classes = null)
        {
            var e = new VisualElement(); AddClasses(e, classes); parent.Add(e); return e;
        }
        Label Text(VisualElement parent, string text, string classes = null)
        {
            var e = new Label(text); AddClasses(e, classes); parent.Add(e); return e;
        }
        Button Button(VisualElement parent, string text, System.Action action, string classes = null)
        {
            var e = new Button(action) { text = text }; AddClasses(e, classes); parent.Add(e); return e;
        }
        static void AddClasses(VisualElement element, string classes)
        {
            if (classes != null) foreach (string c in classes.Split(' ')) element.AddToClassList(c);
        }
        void NewPage()
        {
            root.Clear(); overlay = null;
            app = Element(root, "app");
            previousSize = Vector2Int.zero; UpdateSafeArea();
        }
        void Header(bool inFight)
        {
            var row = Element(app, "header row");
            Text(row, "ARM<color=#de7849>ORB</color>", "logo");
            if (inFight) timer = Text(row, "00:00", "timer");
            else Text(row, "ПЕСОЧНИЦА  /  01", "badge");
        }
        public void ShowSetup()
        {
            NewPage(); Header(false);
            Text(app, "Выбери оружие.\nПусть решит арена.", "hero");
            Text(app, "Два шара. Никакого управления. Только физика.", "subtitle");
            var illustration = new Image { image = game.setupIllustration, scaleMode = ScaleMode.ScaleToFit };
            illustration.AddToClassList("illustration"); app.Add(illustration);
            var section = Element(app, "row section-heading");
            Text(section, "НАСТРОЙКА ПОЕДИНКА", "eyebrow");
            Text(section, "5 видов оружия", "muted small");
            var cards = Element(app, "row cards");
            for (int side = 0; side < 2; side++) AddWeaponCard(cards, side);
            Text(app, "Каждое попадание усиливает оружие.\nКаждый поединок начинается с нуля.", "hint");
            Element(app, "spacer");
            Button(app, "НАЧАТЬ ПОЕДИНОК  →", () => game.StartFight(), "primary").name = "start-fight";
            var footer = Element(app, "row footer");
            Button(footer, "Как это работает", ShowRules, "text-button");
            sound = Button(footer, "", game.ToggleSound, "text-button"); RefreshSound();
        }
        void AddWeaponCard(VisualElement parent, int side)
        {
            var card = Element(parent, side == 0 ? "fighter-card you" : "fighter-card enemy");
            card.name = side == 0 ? "player-settings" : "enemy-settings";
            Text(card, side == 0 ? "ТВОЙ ШАР" : "ПРОТИВНИК", side == 0 ? "eyebrow coral" : "eyebrow teal");
            var picker = Element(card, "row picker");
            Image icon = null; Label name = null, description = null;
            void Refresh()
            {
                var weapon = game.weapons[game.Selection[side]];
                icon.sprite = weapon.sprite; name.text = weapon.displayName; description.text = weapon.description;
            }
            Button(picker, "‹", () => { game.Selection[side] = (game.Selection[side] + 4) % 5; Refresh(); }, "arrow").name = $"weapon-{side}-previous";
            icon = new Image { scaleMode = ScaleMode.ScaleToFit }; icon.AddToClassList("weapon-icon"); picker.Add(icon);
            Button(picker, "›", () => { game.Selection[side] = (game.Selection[side] + 1) % 5; Refresh(); }, "arrow").name = $"weapon-{side}-next";
            name = Text(card, "", "weapon-name");
            description = Text(card, "", "weapon-description muted");
            var health = new IntegerField("ЗДОРОВЬЕ") { value = game.Health[side], isDelayed = true };
            health.AddToClassList("health-input"); health.name = $"health-{side}";
            health.RegisterValueChangedCallback(e =>
            {
                game.Health[side] = Mathf.Clamp(e.newValue, 1, 99999);
                health.SetValueWithoutNotify(game.Health[side]);
            });
            card.Add(health); Refresh();
        }
        public void ShowFight()
        {
            NewPage(); Header(true);
            var versus = Element(app, "row versus");
            Text(versus, game.weapons[game.Selection[0]].displayName, "match-weapon coral");
            Text(versus, "VS", "vs");
            Text(versus, game.weapons[game.Selection[1]].displayName, "match-weapon teal");
            var healthRow = Element(app, "row health-row");
            for (int i = 0; i < 2; i++)
            {
                var panel = Element(healthRow, "health-panel");
                var line = Element(panel, "row");
                Text(line, i == 0 ? "ТВОЙ ШАР" : "ПРОТИВНИК", "eyebrow");
                hp[i] = Text(line, "100", i == 0 ? "hp coral" : "hp teal");
                var track = Element(panel, "health-track");
                healthBars[i] = Element(track, i == 0 ? "health-fill fill-coral" : "health-fill fill-teal");
                poison[i] = Text(panel, "", "poison");
            }
            var field = new Image { image = game.ArenaTexture, scaleMode = ScaleMode.StretchToFill };
            field.AddToClassList("arena"); field.name = "arena"; app.Add(field);
            field.RegisterCallback<GeometryChangedEvent>(e =>
            {
                // Fit a square while leaving room for all controls on short portrait displays.
                float available = app.resolvedStyle.height - app.resolvedStyle.paddingTop - app.resolvedStyle.paddingBottom - 400;
                float side = Mathf.Min(e.newRect.width, Mathf.Max(200, available));
                field.style.height = side;
            });
            var statRow = Element(app, "row stat-row");
            stats[0] = Text(statRow, "", "stat coral"); stats[1] = Text(statRow, "", "stat teal");
            Text(app, "ОРУЖИЕ РАСТЁТ С КАЖДЫМ ПОПАДАНИЕМ", "battle-note");
            Element(app, "spacer");
            var controls = Element(app, "row controls");
            Button(controls, "← Настройки", game.BackToSetup, "secondary");
            Button(controls, "Ⅱ  Пауза", game.TogglePause, "secondary").name = "pause";
            var footer = Element(app, "row footer");
            Text(footer, "БОЙ #" + game.LastSeed, "seed");
            sound = Button(footer, "", game.ToggleSound, "text-button"); RefreshSound();
            RefreshFight();
        }
        public void RefreshFight()
        {
            if (game.Simulation == null || hp[0] == null) return;
            float elapsed = game.Simulation.Elapsed;
            timer.text = $"{(int)elapsed / 60:00}:{(int)elapsed % 60:00}";
            for (int i = 0; i < 2; i++)
            {
                var orb = game.Simulation.Orbs[i];
                hp[i].text = Mathf.CeilToInt(orb.Health.Current).ToString();
                healthBars[i].style.width = Length.Percent(orb.Health.Current / orb.Health.Maximum * 100);
                stats[i].text = orb.Weapon.Stat;
                poison[i].text = orb.Health.PoisonPerSecond > 0 ? $"ЯД  −{orb.Health.PoisonPerSecond:0.#}/с" : "";
            }
        }
        public void RefreshSound() { if (sound != null) sound.text = game.feedback.Muted ? "Звук: выкл" : "Звук: вкл"; }
        VisualElement Dialog(string eyebrow, string title, string description)
        {
            CloseOverlay(); overlay = Element(root, "overlay");
            var card = Element(overlay, "dialog");
            Text(card, eyebrow, "eyebrow muted"); Text(card, title, "dialog-title");
            Text(card, description, "dialog-description"); return card;
        }
        void CloseOverlay() { overlay?.RemoveFromHierarchy(); overlay = null; }
        public void ShowPause(bool paused)
        {
            if (!paused) { CloseOverlay(); return; }
            var card = Dialog("ПЕРЕРЫВ", "Арена подождёт.", "Бой приостановлен.");
            Button(card, "ПРОДОЛЖИТЬ", game.TogglePause, "primary").name = "resume";
            Button(card, "Настроить другой бой", game.BackToSetup, "secondary dialog-secondary");
        }
        public void ShowResult()
        {
            RefreshFight();
            bool draw = game.Simulation.Result == MatchResult.Draw;
            bool won = game.Simulation.Result == MatchResult.Player;
            var winner = game.Simulation.Orbs[won ? 0 : 1];
            string details = draw ? "Оба шара выбыли одновременно." : $"{game.weapons[game.Selection[won ? 0 : 1]].displayName} · Осталось здоровья: {Mathf.CeilToInt(winner.Health.Current)}";
            var card = Dialog("ПОЕДИНОК ЗАВЕРШЁН", draw ? "Ничья." : won ? "Твой шар победил." : "Противник победил.", details);
            Text(card, $"Время боя: {game.Simulation.Elapsed:0.0} с", "muted");
            Button(card, "ЕЩЁ ОДИН БОЙ  →", () => game.StartFight(), "primary").name = "rematch";
            Button(card, "Выбрать другое оружие", game.BackToSetup, "secondary dialog-secondary");
        }
        void ShowRules()
        {
            var card = Dialog("КАК ИГРАТЬ", "Смотри, как растёт сила.", "Выбери оружие и здоровье обоих шаров. Дальше они сражаются сами.");
            var scroll = new ScrollView(); scroll.style.maxHeight = 440; card.Add(scroll);
            string[] rules = {
                "МЕЧ — +1 к урону за каждое попадание.",
                "КОПЬЁ — +0,5 радиуса к длине за попадание. Стены не останавливают оружие.",
                "КЛИНОК — +30°/с к вращению за попадание.",
                "КОСА — +1 к урону яда в секунду на цели. Яд остаётся до конца боя.",
                "ЩИТ — блокирует оружие и возвращает его урон. Каждый блок усиливает возврат на 25 процентных пунктов.",
                "Столкновение оружий меняет направление вращения. Усиления сбрасываются в новом бою. Ограничения роста нет."
            };
            foreach (string rule in rules) Text(scroll, rule, "rule");
            Button(card, "ПОНЯТНО", CloseOverlay, "primary");
        }
        public void UpdateSafeArea()
        {
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == previousSafe && size == previousSize) return;
            previousSafe = safe; previousSize = size;
            float scale = 720f / Mathf.Max(1, Screen.width);
            if (app == null) return;
            app.style.paddingTop = 22 + (Screen.height - safe.yMax) * scale;
            app.style.paddingBottom = 16 + safe.yMin * scale;
            app.style.paddingLeft = 30 + safe.xMin * scale;
            app.style.paddingRight = 30 + (Screen.width - safe.xMax) * scale;
        }
    }
}
