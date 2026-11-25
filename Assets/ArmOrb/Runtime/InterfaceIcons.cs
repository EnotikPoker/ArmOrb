using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public enum UiSymbol { Heal=100, Shield, Settings, Back, Pause, Play, Coins, Test, Battle, Close, Clock, Trophy }

    // Code-native vector artwork: the same silhouette is used in drafts, builds and inspection.
    public sealed class InterfaceIcon : VisualElement
    {
        public int Symbol { get; }
        public Color Tint { get; }
        public float Progress { get; private set; } = -1;
        public InterfaceIcon(BuildSkill skill) : this((int)skill, SkillColor(skill)) { }
        public InterfaceIcon(UiSymbol symbol) : this((int)symbol,new Color32(196,210,232,255)) { }
        public InterfaceIcon(int symbol,Color tint)
        {
            Symbol=symbol;Tint=tint;pickingMode=PickingMode.Ignore;AddToClassList("vector-icon");generateVisualContent+=Paint;
        }
        public static Color SkillColor(BuildSkill skill) => (int)skill switch {
            0 or 1 or 2 or 4 or 7 or 15=>new Color32(98,224,168,255),
            3 or 5 or 6 or 24=>new Color32(106,177,255,255),
            16 or 17 or 18 or 19 or 23=>new Color32(193,146,255,255),
            _=>new Color32(255,187,103,255) };
        public void SetProgress(float value) { if(Mathf.Abs(value-Progress)<.002f)return;Progress=value;MarkDirtyRepaint(); }
        static Vector2 V(float x,float y)=>new Vector2(x,y);
        void Paint(MeshGenerationContext context)
        {
            if(contentRect.width<1||contentRect.height<1)return;
            var p=context.painter2D;float scale=Mathf.Min(contentRect.width,contentRect.height)/64;
            // Paths use normalized 64×64 coordinates so every screen shares the original artwork.
            Vector2 offset=new Vector2((contentRect.width-64*scale)/2,(contentRect.height-64*scale)/2);
            Vector2 P(float x,float y)=>offset+V(x,y)*scale;
            void Line(params float[] xy){p.BeginPath();p.MoveTo(P(xy[0],xy[1]));for(int i=2;i<xy.Length;i+=2)p.LineTo(P(xy[i],xy[i+1]));p.strokeColor=Tint;p.lineWidth=3.2f*scale;p.Stroke();}
            void Shape(bool fill,params float[] xy){p.BeginPath();p.MoveTo(P(xy[0],xy[1]));for(int i=2;i<xy.Length;i+=2)p.LineTo(P(xy[i],xy[i+1]));p.ClosePath();p.strokeColor=Tint;p.fillColor=new Color(Tint.r,Tint.g,Tint.b,.2f);p.lineWidth=3*scale;if(fill)p.Fill();p.Stroke();}
            void Circle(float x,float y,float radius,float begin=0,float end=360){p.BeginPath();p.Arc(P(x,y),radius*scale,begin,end);p.strokeColor=Tint;p.lineWidth=3*scale;p.Stroke();}
            void Plus(float x,float y,float size=7){Line(x-size,y,x+size,y);Line(x,y-size,x,y+size);}
            void Heart(){p.BeginPath();p.MoveTo(P(32,51));p.BezierCurveTo(P(3,34),P(10,10),P(25,19));p.LineTo(P(32,26));p.LineTo(P(39,19));p.BezierCurveTo(P(54,10),P(61,34),P(32,51));p.ClosePath();p.fillColor=new Color(Tint.r,Tint.g,Tint.b,.17f);p.Fill();p.strokeColor=Tint;p.lineWidth=3*scale;p.Stroke();}
            void Shield(){Shape(true,32,9,51,17,48,38,41,49,32,56,23,49,16,38,13,17);}
            void Blade(){Shape(true,17,45,39,12,49,11,48,22,25,49);Line(15,39,31,51);Line(20,47,13,56);}
            void Arrow(float x,float y){Line(x-16,y+16,x+15,y-15);Line(x+1,y-16,x+16,y-16,x+16,y-1);}
            void Spark(float x,float y,float r){Shape(true,x,y-r,x+r*.28f,y-r*.28f,x+r,y,x+r*.28f,y+r*.28f,x,y+r,x-r*.28f,y+r*.28f,x-r,y,x-r*.28f,y-r*.28f);}
            switch(Symbol)
            {
                case 0:Heart();Plus(32,34,6);break;
                case 1:Heart();Circle(32,32,27,210,335);Line(53,11,58,24,44,21);Plus(32,34,5);break;
                case 2:Heart();Line(10,34,23,34,27,26,33,43,38,33,54,33);break;
                case 3:Circle(32,32,23);Shield();break;
                case 4:Shape(true,32,9,48,32,46,45,39,53,25,53,18,45,16,32);Line(22,40,29,46,40,33);break;
                case 5:Shield();Line(23,25,41,25);Line(23,33,41,33);Line(28,41,36,41);break;
                case 6:Shield();Line(19,20,44,41);Line(17,29,39,49);Line(25,14,48,32);break;
                case 7:Circle(32,32,24,35,320);Line(47,10,57,19,44,24);Shape(true,28,20,39,20,35,31,43,31,25,49,29,35,21,35);break;
                case 8:Blade();Spark(17,17,7);break;
                case 9:Spark(32,32,25);Line(32,20,32,34);Circle(32,43,1.7f);break;
                case 10:Shape(true,14,27,23,16,28,25,36,13,42,27,51,24,48,47,32,54,16,46);Line(23,34,27,37);Line(41,34,37,37);break;
                case 11:Line(14,15,50,51);Line(50,15,14,51);Circle(32,32,13);break;
                case 12:Line(19,51,19,29,11,29,32,10,53,29,45,29,45,51);Line(27,43,37,43);Line(27,51,37,51);break;
                case 13:Shape(true,35,8,15,35,30,35,25,57,50,27,35,27);Circle(49,49,7);break;
                case 14:Circle(32,32,23);Line(32,15,32,32,45,32);Circle(17,50,3);Circle(32,55,3);Circle(47,50,3);break;
                case 15:Circle(32,32,20);Circle(32,32,10);Line(32,5,32,21);Line(32,43,32,59);Line(5,32,21,32);Line(43,32,59,32);break;
                case 16:Shape(true,32,8,42,27,45,20,53,36,48,49,36,56,22,53,12,41,17,26,23,34);Shape(false,32,32,40,44,32,51,25,44);break;
                case 17:Line(19,11,14,40);Line(33,11,28,40);Line(47,11,42,40);Shape(true,27,42,34,51,27,57,20,51);break;
                case 18:Circle(32,32,23);Heart();Line(13,51,51,13);break;
                case 19:Shield();Line(9,49,54,13);Line(42,13,54,13,54,25);break;
                case 20:Blade();Line(37,40,51,54);Line(44,35,58,49);break;
                case 21:Arrow(31,32);Line(9,46,23,46);Line(7,54,18,54);break;
                case 22:Blade();Circle(32,32,27,210,345);Line(53,10,59,24,45,21);break;
                case 23:Line(23,53,23,16);p.BeginPath();p.MoveTo(P(12,16));p.BezierCurveTo(P(40,1),P(58,22),P(48,37));p.BezierCurveTo(P(43,20),P(30,16),P(12,16));p.ClosePath();p.strokeColor=Tint;p.lineWidth=3*scale;p.Stroke();Circle(43,47,5);break;
                case 24:Shield();Plus(32,28);Line(24,45,40,45);break;
                case 25:Shape(true,13,29,22,23,26,29,29,20,35,26,39,22,49,31,48,45,38,53,22,50,13,40);Line(17,12,24,18);Line(45,12,40,18);break;
                case 26:Line(20,12,44,32,20,52);Line(20,12,20,52);Line(14,22,43,22,49,17);Line(14,42,43,42,49,37);break;
                case 27:Arrow(31,32);Line(17,10,17,27);Line(31,37,31,54);Line(45,39,45,54);break;
                case 28:Circle(32,32,11);Circle(32,32,20,210,515);Circle(32,32,28,35,145);Circle(32,32,28,215,325);break;
                case 29:Shape(true,32,9,37,25,54,22,42,34,49,50,32,43,17,52,22,35,9,23,27,25);Line(49,10,57,10,57,20);break;
                case 100:Heart();Plus(32,34);break;
                case 101:Shield();Plus(32,31);break;
                case 102:Circle(32,32,11);Circle(32,32,21);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(32+21*Mathf.Cos(a),32+21*Mathf.Sin(a),32+28*Mathf.Cos(a),32+28*Mathf.Sin(a));}break;
                case 103:Line(39,14,21,32,39,50);Line(22,32,54,32);break;
                case 104:Shape(true,19,13,26,13,26,51,19,51);Shape(true,38,13,45,13,45,51,38,51);break;
                case 105:Shape(true,23,12,51,32,23,52);break;
                case 106:Circle(32,32,23);Circle(32,32,17);Line(37,20,27,20,24,25,27,32,37,32,40,39,36,44,25,44);Line(32,15,32,49);break;
                case 107:Line(23,10,41,10);Line(27,10,27,26,13,49,16,54,48,54,51,49,37,26,37,10);Line(21,40,43,40);Circle(32,44,2);break;
                case 108:Blade();Line(14,14,50,50);Line(10,19,20,9);Line(45,55,55,45);break;
                case 109:Line(17,17,47,47);Line(47,17,17,47);break;
                case 110:Circle(32,34,22);Line(32,20,32,34,42,40);Line(25,7,39,7);break;
                case 111:Shape(true,20,10,44,10,43,30,37,41,27,41,21,30);Line(20,16,10,16,12,30,23,35);Line(44,16,54,16,52,30,41,35);Line(32,41,32,53);Line(22,54,42,54);break;
            }
            if(Progress>=0){p.BeginPath();p.Arc(P(32,32),30*scale,-90,-90+Mathf.Clamp01(Progress)*360);p.strokeColor=new Color32(98,224,168,255);p.lineWidth=3*scale;p.Stroke();}
        }
    }
}
