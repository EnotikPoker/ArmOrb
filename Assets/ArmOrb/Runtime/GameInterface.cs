using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed class GameInterface
    {
        readonly ArmOrbGame game;
        readonly VisualElement root;
        VisualElement app,overlay;
        Button sound,pause;
        Label timer;
        readonly Label[] hp=new Label[2],stats=new Label[2],poison=new Label[2];
        readonly List<(Button button,float speed)> speedButtons=new List<(Button,float)>();
        Rect previousSafe;
        Vector2Int previousSize;
        public GameInterface(ArmOrbGame game)
        {
            this.game=game; root=game.document.rootVisualElement;
            root.styleSheets.Add(game.styleSheet); root.style.unityFont=game.uiFont;
        }
        VisualElement Element(VisualElement parent,string classes=null)
        { var e=new VisualElement(); Classes(e,classes); parent.Add(e); return e; }
        Label Text(VisualElement parent,string text,string classes=null)
        { var e=new Label(text); Classes(e,classes); parent.Add(e); return e; }
        Button Button(VisualElement parent,string text,System.Action action,string classes=null)
        { var e=new Button(action){text=text}; Classes(e,classes); parent.Add(e); return e; }
        static void Classes(VisualElement e,string classes)
        { if(classes!=null) foreach(string c in classes.Split(' ')) e.AddToClassList(c); }
        static void Selected(VisualElement e,bool selected) => e.EnableInClassList("selected",selected);
        void NewPage()
        {
            root.Clear(); overlay=null; speedButtons.Clear();
            app=Element(root,"app"); previousSize=Vector2Int.zero; UpdateSafeArea();
        }
        public void ShowSetup()
        {
            NewPage();
            var header=Element(app,"row header"); Text(header,"ARMORB","logo"); Text(header,"0.2","version");
            var content=Element(app,"setup-content");
            var cards=Element(content,"row fighter-cards");
            for(int i=0;i<2;i++) AddWeaponCard(cards,i);
            Text(content,"Арена","setting-label");
            var sizes=Element(content,"row segments");
            string[] names={"Маленькая","Средняя","Большая"};
            var buttons=new Button[3];
            for(int i=0;i<3;i++)
            {
                int index=i;
                buttons[i]=Button(sizes,names[i],()=>
                {
                    game.Options.arena=(ArenaSize)index; game.Options.Save();
                    for(int n=0;n<3;n++) Selected(buttons[n],n==index);
                },"segment");
                Selected(buttons[i],(int)game.Options.arena==i);
            }
            Text(content,"Скорость","setting-label"); AddSpeedControl(content);
            var gravity=new Toggle("Гравитация"){value=game.Options.gravity}; gravity.AddToClassList("gravity");
            gravity.RegisterValueChangedCallback(e=>{game.Options.gravity=e.newValue;game.Options.Save();}); content.Add(gravity);
            Button(content,"Начать",()=>game.StartFight(),"primary").name="start-fight";
            var footer=Element(app,"row footer");
            Button(footer,"Правила",ShowRules,"text-button"); sound=Button(footer,"",game.ToggleSound,"text-button"); RefreshSound();
        }
        void AddWeaponCard(VisualElement parent,int side)
        {
            var card=Element(parent,side==0?"fighter-card player":"fighter-card enemy");
            Text(card,side==0?"Игрок":"Противник",side==0?"side-name coral":"side-name teal");
            var icon=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore}; icon.AddToClassList("weapon-preview");card.Add(icon);
            var name=Text(card,"","weapon-name");
            var row=Element(card,"row weapons");
            var buttons=new Button[5];
            void Refresh()
            {
                var definition=game.weapons[game.Selection[side]];
                icon.sprite=definition.sprite; name.text=definition.displayName;
                for(int i=0;i<5;i++) if(buttons[i]!=null) Selected(buttons[i],game.Selection[side]==i);
            }
            for(int i=0;i<5;i++)
            {
                int selected=i;
                buttons[i]=Button(row,"",()=>{game.Selection[side]=selected;Refresh();},"weapon-choice");
                buttons[i].tooltip=game.weapons[i].displayName; buttons[i].name=$"weapon-{side}-{i}";
                var image=new Image{sprite=game.weapons[i].sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};
                image.AddToClassList("weapon-choice-image");buttons[i].Add(image);
            }
            var health=new IntegerField("HP"){value=game.Health[side],isDelayed=true,keyboardType=TouchScreenKeyboardType.NumberPad};
            health.AddToClassList("health-input");health.name=$"health-{side}";
            health.RegisterValueChangedCallback(e=>{game.Health[side]=Mathf.Clamp(e.newValue,1,99999);health.SetValueWithoutNotify(game.Health[side]);});
            card.Add(health); Refresh();
        }
        void AddSpeedControl(VisualElement parent)
        {
            var row=Element(parent,"row segments speed-control");
            foreach(float value in new[]{0.5f,1,2})
            {
                float speed=value;
                var button=Button(row,value==0.5f?"x0.5":value==1?"x1":"x2",()=>game.SetPlaybackSpeed(speed),"segment");
                button.name=value==0.5f?"speed-half":value==1?"speed-normal":"speed-double";
                speedButtons.Add((button,value));
            }
            RefreshSpeed();
        }
        public void RefreshSpeed()
        { foreach(var pair in speedButtons) Selected(pair.button,Mathf.Approximately(pair.speed,game.Options.playbackSpeed)); }
        public void ShowFight()
        {
            NewPage();
            var header=Element(app,"row header");
            Button(header,"Настройки",game.BackToSetup,"header-button");timer=Text(header,"00:00","timer");
            pause=Button(header,"Пауза",game.TogglePause,"header-button");pause.name="pause";
            var versus=Element(app,"row versus");
            for(int i=0;i<2;i++)
            {
                var side=Element(versus,"row fighter-title");
                var icon=new Image{sprite=game.weapons[game.Selection[i]].sprite,scaleMode=ScaleMode.ScaleToFit};icon.AddToClassList("title-icon");side.Add(icon);
                Text(side,game.weapons[game.Selection[i]].displayName,i==0?"match-weapon coral":"match-weapon teal");
                if(i==0) Text(versus,"VS","vs");
            }
            var health=Element(app,"row health-row");
            for(int i=0;i<2;i++)
            {
                var panel=Element(health,"health-panel");var line=Element(panel,"row");
                Text(line,i==0?"Игрок":"Противник","small");hp[i]=Text(line,"",i==0?"hp coral":"hp teal");
                poison[i]=Text(panel,"","poison");
            }
            var holder=Element(app,"arena-holder");
            var field=new Image{image=game.ArenaTexture,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore};field.AddToClassList("arena");field.name="arena";holder.Add(field);
            holder.RegisterCallback<GeometryChangedEvent>(e=>
            {
                float size=Mathf.Max(1,Mathf.Min(e.newRect.width,e.newRect.height));
                field.style.width=size;field.style.height=size;
            });
            var statRow=Element(app,"row stat-row");
            stats[0]=Text(statRow,"","stat coral");stats[1]=Text(statRow,"","stat teal");
            AddSpeedControl(app);
            var footer=Element(app,"row footer");
            string[] sizes={"Маленькая","Средняя","Большая"};
            Text(footer,sizes[(int)game.Options.arena]+(game.Options.gravity?" · Гравитация":""),"small muted");
            sound=Button(footer,"",game.ToggleSound,"text-button");RefreshSound();RefreshFight();
        }
        public void RefreshFight()
        {
            if(game.Simulation==null || hp[0]==null) return;
            float elapsed=game.Simulation.Elapsed;timer.text=$"{(int)elapsed/60:00}:{(int)elapsed%60:00}";
            for(int i=0;i<2;i++)
            {
                var orb=game.Simulation.Orbs[i];hp[i].text=Mathf.CeilToInt(orb.Health.Current).ToString();stats[i].text=orb.Weapon.Stat;
                poison[i].text=orb.Health.PoisonPerSecond>0?$"Яд −{orb.Health.PoisonPerSecond:0.#}/с":"";
            }
        }
        public void RefreshSound() { if(sound!=null) sound.text=game.feedback.Muted?"Звук: выкл":"Звук: вкл"; }
        VisualElement Dialog(string title,string description=null)
        {
            CloseOverlay();overlay=Element(root,"overlay");var card=Element(overlay,"dialog");
            Text(card,title,"dialog-title");if(!string.IsNullOrEmpty(description)) Text(card,description,"dialog-description");return card;
        }
        void CloseOverlay(){overlay?.RemoveFromHierarchy();overlay=null;}
        public void ShowPause(bool paused)
        {
            if(!paused){CloseOverlay();return;}
            var card=Dialog("Пауза");
            Button(card,"Продолжить",game.TogglePause,"primary").name="resume";
            Button(card,"Настройки",game.BackToSetup,"secondary");
        }
        public void ShowResult()
        {
            RefreshFight();bool draw=game.Simulation.Result==MatchResult.Draw,won=game.Simulation.Result==MatchResult.Player;
            var winner=game.Simulation.Orbs[won?0:1];
            string description=draw?"Оба шара выбыли.":$"{game.weapons[game.Selection[won?0:1]].displayName} · HP {Mathf.CeilToInt(winner.Health.Current)}";
            var card=Dialog(draw?"Ничья":won?"Победитель: игрок":"Победитель: противник",description);
            Text(card,$"Время: {game.Simulation.Elapsed:0.0} с","small");
            Button(card,"Повторить",()=>game.StartFight(),"primary").name="rematch";
            Button(card,"Настройки",game.BackToSetup,"secondary");
        }
        void ShowRules()
        {
            var card=Dialog("Правила");var scroll=new ScrollView();scroll.style.maxHeight=450;card.Add(scroll);
            string[] rules={"Меч: +1 к урону за попадание.","Копьё: +0,5 радиуса к длине за попадание.",
                "Клинок: +30°/с к вращению за попадание.","Коса: +1 к урону яда в секунду на цели за попадание. Яд действует до конца боя.",
                "Щит: возвращает прямой урон при блоке. Каждый блок добавляет 25 процентных пунктов к возврату.",
                "Урон наносит всё видимое оружие. Для повторного удара нужен разрыв контакта. Усиления сбрасываются в новом бою."};
            foreach(string rule in rules) Text(scroll,rule,"rule");Button(card,"Закрыть",CloseOverlay,"primary");
        }
        public void UpdateSafeArea()
        {
            Rect safe=Screen.safeArea;var size=new Vector2Int(Screen.width,Screen.height);
            if(safe==previousSafe && size==previousSize)return;previousSafe=safe;previousSize=size;
            float scale=720f/Mathf.Max(1,Screen.width);if(app==null)return;
            app.style.paddingTop=14+(Screen.height-safe.yMax)*scale;app.style.paddingBottom=10+safe.yMin*scale;
            app.style.paddingLeft=22+safe.xMin*scale;app.style.paddingRight=22+(Screen.width-safe.xMax)*scale;
        }
    }
}
