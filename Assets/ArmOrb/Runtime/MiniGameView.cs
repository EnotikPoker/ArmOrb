using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    // Only this element captures game gestures; arena and navigation remain independent.
    public sealed class MiniGameView : VisualElement
    {
        readonly MiniGameModel model;
        readonly Func<bool> active;
        readonly Action<int> reward;
        readonly List<Vector2> stroke = new List<Vector2>();
        readonly List<int> chain = new List<int>();
        int pointer = -1;
        float started;
        static readonly Color[] colors = { new Color32(228,91,107,255), new Color32(21,162,172,255), new Color32(128,98,210,255), new Color32(218,156,36,255) };
        public MiniGameView(MiniGameModel model, Func<bool> active, Action<int> reward)
        {
            this.model = model; this.active = active; this.reward = reward;
            name = "mini-game-canvas"; AddToClassList("mini-canvas");
            generateVisualContent += Paint;
            RegisterCallback<PointerDownEvent>(Down);
            RegisterCallback<PointerMoveEvent>(Move);
            RegisterCallback<PointerUpEvent>(Up);
            RegisterCallback<PointerCancelEvent>(_ => CancelGesture());
            RegisterCallback<PointerCaptureOutEvent>(_ => { pointer = -1; stroke.Clear(); chain.Clear(); });
        }
        public void CancelGesture()
        {
            int previous = pointer; pointer = -1; stroke.Clear(); chain.Clear();
            if (previous >= 0 && this.HasPointerCapture(previous)) this.ReleasePointer(previous);
            MarkDirtyRepaint();
        }
        public void Refresh() { if (!active()) CancelGesture(); MarkDirtyRepaint(); }
        Rect DrawArea()
        {
            float side = Mathf.Min(contentRect.height-20, contentRect.width*.62f);
            return new Rect(contentRect.width*.66f-side*.5f, (contentRect.height-side)*.5f, side, side);
        }
        Rect TileArea()
        {
            float side = Mathf.Min(contentRect.width-12,contentRect.height-12);
            return new Rect((contentRect.width-side)*.5f,(contentRect.height-side)*.5f,side,side);
        }
        int Tile(Vector2 p)
        { var r = TileArea(); if (!r.Contains(p)) return -1; return Mathf.Min(3,(int)((p.y-r.y)/r.height*4))*4+Mathf.Min(3,(int)((p.x-r.x)/r.width*4)); }
        Vector2 Normalized(Vector2 p) { var r = DrawArea(); return new Vector2(Mathf.Clamp01((p.x-r.x)/r.width),Mathf.Clamp01((p.y-r.y)/r.height)); }
        void Down(PointerDownEvent e)
        {
            if (!active() || pointer >= 0 || e.button != 0) return;
            Vector2 p = e.localPosition;
            if (model.Kind == MiniGameKind.Rhythm) { reward(model.TapRhythm()); e.StopPropagation(); return; }
            if (model.Kind == MiniGameKind.Drawing && !DrawArea().Contains(p)) return;
            if (model.Kind == MiniGameKind.Tiles && Tile(p) < 0) return;
            pointer = e.pointerId; started = Time.unscaledTime; this.CapturePointer(pointer);
            if (model.Kind == MiniGameKind.Drawing) stroke.Add(Normalized(p)); else chain.Add(Tile(p));
            MarkDirtyRepaint(); e.StopPropagation();
        }
        void Move(PointerMoveEvent e)
        {
            if (e.pointerId != pointer) return;
            if (!active()) { CancelGesture(); return; }
            Vector2 p = e.localPosition;
            if (model.Kind == MiniGameKind.Drawing)
            { var point = Normalized(p); if (stroke.Count < 1024 && Vector2.Distance(stroke[stroke.Count-1],point) > .004f) stroke.Add(point); }
            else
            {
                int tile = Tile(p);
                if (chain.Count > 1 && tile == chain[chain.Count-2]) chain.RemoveAt(chain.Count-1);
                else if (model.CanAppend(chain,tile)) chain.Add(tile);
            }
            MarkDirtyRepaint(); e.StopPropagation();
        }
        void Up(PointerUpEvent e)
        {
            if (e.pointerId != pointer) return;
            if (active())
            {
                if (model.Kind == MiniGameKind.Drawing) { stroke.Add(Normalized(e.localPosition)); reward(model.SubmitDrawing(stroke,Time.unscaledTime-started)); }
                else reward(model.SubmitChain(chain));
            }
            CancelGesture(); e.StopPropagation();
        }
        static void Path(Painter2D p, IReadOnlyList<Vector2> points, Color color, float width, bool close = false, bool fill = false)
        {
            if (points.Count == 0) return;
            p.BeginPath(); p.MoveTo(points[0]); for (int i = 1; i < points.Count; i++) p.LineTo(points[i]); if (close) p.ClosePath();
            if (fill) { p.fillColor = color; p.Fill(); } else { p.strokeColor = color; p.lineWidth = width; p.Stroke(); }
        }
        static void Box(Painter2D p, Rect r, Color color, bool fill)
        { Path(p,new[] { new Vector2(r.xMin,r.yMin), new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax) },color,2,true,fill); }
        static List<Vector2> Transform(IReadOnlyList<Vector2> source, Rect r)
        { var result = new List<Vector2>(source.Count); foreach (var v in source) result.Add(new Vector2(r.x+v.x*r.width,r.y+v.y*r.height)); return result; }
        void Paint(MeshGenerationContext context)
        {
            if (contentRect.width < 10 || contentRect.height < 10) return;
            var p = context.painter2D;
            if (model.Kind == MiniGameKind.Drawing)
            {
                var area = DrawArea(); Box(p,area,new Color(.84f,.84f,.82f),false);
                float side = Mathf.Min(contentRect.width*.25f,contentRect.height*.6f);
                var example = new Rect(contentRect.width*.01f,(contentRect.height-side)*.5f,side,side);
                Path(p,Transform(MiniGameModel.Template(model.Data.drawingTarget),example),new Color(.2f,.2f,.2f),4);
                Path(p,Transform(stroke,area),new Color(.87f,.24f,.31f),4);
            }
            else if (model.Kind == MiniGameKind.Tiles)
            {
                var area = TileArea(); float size = area.width/4;
                for (int i = 0; i < 16; i++)
                {
                    var r = new Rect(area.x+i%4*size+3,area.y+i/4*size+3,size-6,size-6);
                    Color c = colors[model.Data.tiles[i]]; Box(p,r,c,true);
                    if (chain.Contains(i)) { r.xMin += 3; r.xMax -= 3; r.yMin += 3; r.yMax -= 3; Box(p,r,Color.white,false); }
                    // Shape marks keep the four tile types distinct without relying on colour.
                    int shape = model.Data.tiles[i]; var symbol = new Rect(r.center.x-size*.16f,r.center.y-size*.16f,size*.32f,size*.32f);
                    var points = shape == 0 ? MiniGameModel.Template(5) : shape == 1 ? MiniGameModel.Template(3) : shape == 2 ? MiniGameModel.Template(4) : MiniGameModel.Template(1);
                    Path(p,Transform(points,symbol),Color.white,2);
                }
                var line = new List<Vector2>(); foreach (int i in chain) line.Add(new Vector2(area.x+(i%4+.5f)*size,area.y+(i/4+.5f)*size));
                Path(p,line,Color.white,3);
            }
            else
            {
                var track = new Rect(24,contentRect.height*.35f,contentRect.width-48,54);
                Box(p,track,new Color(.89f,.89f,.87f),true);
                var target = new Rect(track.x+(model.Data.rhythmTarget-.12f)*track.width,track.y,track.width*.24f,track.height);
                Box(p,target,new Color(.64f,.84f,.75f),true);
                Box(p,new Rect(track.x+(model.Data.rhythmTarget-.045f)*track.width,track.y,track.width*.09f,track.height),new Color(.2f,.65f,.47f),true);
                float x = track.x+model.Cursor*track.width;
                Path(p,new[] { new Vector2(x,track.y-12),new Vector2(x,track.yMax+12) },model.Data.rhythmTapped ? Color.gray : Color.black,5);
            }
        }
    }
}
