// STZ WireCut — изменение детали через размер (как во Fusion 360):
// щелчок по числу размера → ввод нового значения → геометрия перестраивается
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WireCut
{
    public static class DimEdit
    {
        const double Tol = 1e-4;

        /// <summary>Связанная группа: все элементы, соединённые концами с исходным.</summary>
        public static List<Seg> Group(List<Seg> ents, Seg start)
        {
            List<Seg> g = new List<Seg> { start };
            bool grown = true;
            while (grown)
            {
                grown = false;
                foreach (Seg s in ents)
                {
                    if (g.Contains(s) || s.IsCircle) continue;
                    foreach (Seg q in g)
                    {
                        if (q.IsCircle) continue;
                        if (Near(s.Start, q.Start) || Near(s.Start, q.End) || Near(s.End, q.Start) || Near(s.End, q.End)) { g.Add(s); grown = true; break; }
                    }
                }
            }
            return g;
        }
        static bool Near(V2 a, V2 b) { return V2.Dist(a, b) < 0.01; }

        /// <summary>Перенести конец old→nw у всех отрезков (кроме except), чтобы контур не разорвался.</summary>
        static void MovePoint(List<Seg> ents, V2 old, V2 nw, Seg except)
        {
            foreach (Seg s in ents)
            {
                if (s == except || s.Kind != SegKind.Line) continue;
                if (V2.Dist(s.P0, old) < 0.01) s.P0 = nw;
                if (V2.Dist(s.P1, old) < 0.01) s.P1 = nw;
            }
        }

        /// <summary>Применить новое значение размера. Возвращает текст ошибки или null.</summary>
        public static string Apply(CadCanvas cv, Dim d, double v)
        {
            if (d.A == null) return "Размер не привязан к объекту — его можно только посмотреть";
            if (!cv.Ents.Contains(d.A)) return "Объект размера удалён";
            if (v <= 0) return "Значение должно быть больше нуля";
            Seg s = d.A;
            switch (d.K)
            {
                case DimKind.Angle: return "Угловой размер пока изменить нельзя";
                case DimKind.Radius:
                case DimKind.Diameter:
                    {
                        double r = d.K == DimKind.Radius ? v : v / 2;
                        V2 os = s.Start, oe = s.End;
                        s.R = r;
                        if (!s.IsCircle) { MovePoint(cv.Ents, os, s.Start, s); MovePoint(cv.Ents, oe, s.End, s); }
                        return null;
                    }
                case DimKind.Aligned:
                    {
                        if (s.Kind != SegKind.Line) return "Нужен отрезок";
                        V2 old = s.P1, dir = (s.P1 - s.P0).Norm();
                        V2 nw = s.P0 + dir * v;
                        s.P1 = nw;
                        MovePoint(cv.Ents, old, nw, s);
                        return null;
                    }
                default:
                    {
                        // растяжение: точки на «подвижной» стороне (правее / выше) смещаются, остальные стоят
                        bool hor = d.K == DimKind.Horizontal;
                        double a = hor ? Math.Min(d.P1.X, d.P2.X) : Math.Min(d.P1.Y, d.P2.Y);
                        double b = hor ? Math.Max(d.P1.X, d.P2.X) : Math.Max(d.P1.Y, d.P2.Y);
                        if (b - a < Tol) return "Нулевой размер";
                        double delta = (a + v) - b;
                        V2 shift = hor ? new V2(delta, 0) : new V2(0, delta);
                        Fn1<V2, bool> moving = delegate (V2 p) { return (hor ? p.X : p.Y) >= b - Tol; };
                        foreach (Seg q in Group(cv.Ents, s))
                        {
                            if (q.Kind == SegKind.Line)
                            {
                                if (moving(q.P0)) q.P0 = q.P0 + shift;
                                if (moving(q.P1)) q.P1 = q.P1 + shift;
                            }
                            else if (moving(q.Start) && moving(q.End) && moving(q.C)) q.Move(shift);
                        }
                        return null;
                    }
            }
        }
    }

    /// <summary>Изменение размера: поле ввода прямо на размере.</summary>
    public class DimEditCmd : Cmd
    {
        Dim D;
        public DimEditCmd(Dim d) { D = d; }
        public override bool ShowsDims { get { return true; } }
        public override void Begin()
        {
            Cv.EditDim = D;
            if (D.A == null) { F.Msg("  Размер не привязан к объекту — изменить нельзя."); }
            Cv.Invalidate();
        }
        public override string Prompt { get { return "Новое значение размера (сейчас " + D.Text + ") — Enter применить, Esc отмена:"; } }
        public override bool Text(string t)
        {
            double v;
            if (!MainForm.Num(t.Replace("Ø", "").Replace("R", "").Replace("°", ""), out v)) return false;
            F.PushUndo();
            string err = DimEdit.Apply(Cv, D, v);
            if (err != null) { F.Msg("  " + err); return true; }
            F.Msg("  Размер изменён: " + D.Text + " мм");
            F.EndCmd();
            F.GeometryChanged();
            return true;
        }
        public override void Rubber(Graphics g, V2 c)
        {
            if (D.LabelRect.Width <= 0) return;
            string typed = F.InputText.Trim();
            PointF p = new PointF(D.LabelRect.X + D.LabelRect.Width / 2, D.LabelRect.Y + D.LabelRect.Height / 2);
            DimDraw.Field(g, Cv, Cv.ToWorld(System.Drawing.Point.Round(p)), (typed.Length > 0 ? typed : D.Text) + "▌", 1);
        }
    }
}
