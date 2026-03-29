using System.Windows;
using System.Windows.Media;

namespace Metraj.Commands
{
    /// <summary>
    /// Geometrik ikon çizimleri: Civil 3D tarzı — gri gövde + renkli aksan detayları.
    /// </summary>
    public static partial class RibbonIconFactory
    {
        /// <summary>Metraj Paneli: gri kağıt + renkli cetvel çizgileri</summary>
        private static void DrawMetrajPanelIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);

            // Kağıt — gri dikdörtgen
            double pw = s * 0.65;
            double ph = s * 0.85;
            double px = m + (s - pw) / 2;
            double py = m + (s - ph) / 2;
            dc.DrawRoundedRectangle(baseBrush, pen, new Rect(px, py, pw, ph), 2, 2);

            // Cetvel çizgileri — renkli
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 2.0 : 1.3);
            double lx1 = px + pw * 0.15;
            double lx2 = px + pw * 0.85;
            double lx3 = px + pw * 0.55; // kısa çizgi

            double ly1 = py + ph * 0.25;
            double ly2 = py + ph * 0.45;
            double ly3 = py + ph * 0.65;

            dc.DrawLine(accentPen, new Point(lx1, ly1), new Point(lx2, ly1));
            dc.DrawLine(accentPen, new Point(lx1, ly2), new Point(lx3, ly2));
            dc.DrawLine(accentPen, new Point(lx1, ly3), new Point(lx2, ly3));
        }

        /// <summary>Uzunluk: gri yatay çizgi + renkli ok uçları ve ölçü işaretleri</summary>
        private static void DrawUzunlukIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            double cy = size / 2.0;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 2.5 : 1.8);

            double x1 = m + s * 0.08;
            double x2 = m + s * 0.92;

            // Ana yatay çizgi — gri
            dc.DrawLine(pen, new Point(x1, cy), new Point(x2, cy));

            // Dikey ölçü işaretleri — gri
            double tickH = s * 0.2;
            dc.DrawLine(pen, new Point(x1, cy - tickH), new Point(x1, cy + tickH));
            dc.DrawLine(pen, new Point(x2, cy - tickH), new Point(x2, cy + tickH));

            // Ok uçları — renkli
            double aw = s * 0.12;
            // Sol ok
            var leftArrow = new StreamGeometry();
            using (var ctx = leftArrow.Open())
            {
                ctx.BeginFigure(new Point(x1, cy), true, true);
                ctx.LineTo(new Point(x1 + aw * 1.5, cy - aw), true, false);
                ctx.LineTo(new Point(x1 + aw * 1.5, cy + aw), true, false);
            }
            leftArrow.Freeze();
            dc.DrawGeometry(accentBrush, null, leftArrow);

            // Sağ ok
            var rightArrow = new StreamGeometry();
            using (var ctx = rightArrow.Open())
            {
                ctx.BeginFigure(new Point(x2, cy), true, true);
                ctx.LineTo(new Point(x2 - aw * 1.5, cy - aw), true, false);
                ctx.LineTo(new Point(x2 - aw * 1.5, cy + aw), true, false);
            }
            rightArrow.Freeze();
            dc.DrawGeometry(accentBrush, null, rightArrow);

            // Orta ölçü çizgileri — renkli (kısa)
            double midX = size / 2.0;
            double shortTick = s * 0.12;
            dc.DrawLine(accentPen, new Point(midX, cy - shortTick), new Point(midX, cy + shortTick));
        }

        /// <summary>Alan: gri dörtgen polygon + renkli tarama dolgusu</summary>
        private static void DrawAlanIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);

            // Düzensiz dörtgen — gri kontur + yarı saydam renkli dolgu
            var colors = IconPalette.GetColors(IconType.Alan);
            var fillBrush = new SolidColorBrush(Color.FromArgb(0x50, colors.Primary.R, colors.Primary.G, colors.Primary.B));
            fillBrush.Freeze();

            var polygon = new StreamGeometry();
            using (var ctx = polygon.Open())
            {
                ctx.BeginFigure(new Point(m + s * 0.15, m + s * 0.20), true, true);
                ctx.LineTo(new Point(m + s * 0.75, m + s * 0.10), true, false);
                ctx.LineTo(new Point(m + s * 0.90, m + s * 0.60), true, false);
                ctx.LineTo(new Point(m + s * 0.60, m + s * 0.90), true, false);
                ctx.LineTo(new Point(m + s * 0.10, m + s * 0.70), true, false);
            }
            polygon.Freeze();
            dc.DrawGeometry(fillBrush, pen, polygon);

            // Tarama çizgileri — renkli
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 1.2 : 0.8);
            dc.DrawLine(accentPen, new Point(m + s * 0.25, m + s * 0.35), new Point(m + s * 0.70, m + s * 0.25));
            dc.DrawLine(accentPen, new Point(m + s * 0.18, m + s * 0.52), new Point(m + s * 0.82, m + s * 0.42));
            dc.DrawLine(accentPen, new Point(m + s * 0.20, m + s * 0.68), new Point(m + s * 0.75, m + s * 0.60));
        }

        /// <summary>Toplama: gri metin satırları + renkli sigma (Σ) sembolü</summary>
        private static void DrawToplamaIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.0 : 1.3);
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 2.5 : 1.8);

            // Metin satırları — gri (sağ tarafta)
            double lx1 = m + s * 0.45;
            double lx2 = m + s * 0.88;
            dc.DrawLine(pen, new Point(lx1, m + s * 0.20), new Point(lx2, m + s * 0.20));
            dc.DrawLine(pen, new Point(lx1, m + s * 0.40), new Point(lx2, m + s * 0.40));
            dc.DrawLine(pen, new Point(lx1, m + s * 0.60), new Point(lx2, m + s * 0.60));

            // Toplam çizgisi — renkli
            dc.DrawLine(accentPen, new Point(lx1, m + s * 0.78), new Point(lx2, m + s * 0.78));

            // Sigma (Σ) sembolü — renkli (sol tarafta)
            double sx = m + s * 0.08;
            double sw = s * 0.28;
            double sy = m + s * 0.15;
            double sh = s * 0.70;
            double smid = sy + sh / 2;

            var sigma = new StreamGeometry();
            using (var ctx = sigma.Open())
            {
                ctx.BeginFigure(new Point(sx + sw, sy), false, false);
                ctx.LineTo(new Point(sx, sy), true, false);
                ctx.LineTo(new Point(sx + sw * 0.55, smid), true, false);
                ctx.LineTo(new Point(sx, sy + sh), true, false);
                ctx.LineTo(new Point(sx + sw, sy + sh), true, false);
            }
            sigma.Freeze();
            dc.DrawGeometry(null, accentPen, sigma);
        }

        /// <summary>Yol Metraj: gri yol profili eğrisi + renkli istasyon noktaları</summary>
        private static void DrawYolMetrajIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 2.0 : 1.5);

            // Yol profili — gri eğri
            var road = new StreamGeometry();
            using (var ctx = road.Open())
            {
                ctx.BeginFigure(new Point(m + s * 0.05, m + s * 0.65), false, false);
                ctx.BezierTo(
                    new Point(m + s * 0.20, m + s * 0.30),
                    new Point(m + s * 0.40, m + s * 0.20),
                    new Point(m + s * 0.55, m + s * 0.45), true, false);
                ctx.BezierTo(
                    new Point(m + s * 0.70, m + s * 0.70),
                    new Point(m + s * 0.80, m + s * 0.35),
                    new Point(m + s * 0.95, m + s * 0.40), true, false);
            }
            road.Freeze();
            dc.DrawGeometry(null, pen, road);

            // İstasyon noktaları — renkli daireler
            double r = s * 0.07;
            dc.DrawEllipse(accentBrush, null, new Point(m + s * 0.15, m + s * 0.48), r, r);
            dc.DrawEllipse(accentBrush, null, new Point(m + s * 0.40, m + s * 0.28), r, r);
            dc.DrawEllipse(accentBrush, null, new Point(m + s * 0.55, m + s * 0.45), r, r);
            dc.DrawEllipse(accentBrush, null, new Point(m + s * 0.75, m + s * 0.52), r, r);

            // Dikey istasyon çizgileri — renkli
            double baseY = m + s * 0.85;
            var thinPen = CreateSymbolPen(accentBrush, isLarge ? 1.2 : 0.8);
            dc.DrawLine(thinPen, new Point(m + s * 0.15, m + s * 0.48 + r), new Point(m + s * 0.15, baseY));
            dc.DrawLine(thinPen, new Point(m + s * 0.40, m + s * 0.28 + r), new Point(m + s * 0.40, baseY));
            dc.DrawLine(thinPen, new Point(m + s * 0.55, m + s * 0.45 + r), new Point(m + s * 0.55, baseY));
            dc.DrawLine(thinPen, new Point(m + s * 0.75, m + s * 0.52 + r), new Point(m + s * 0.75, baseY));

            // Taban çizgisi — gri
            dc.DrawLine(pen, new Point(m + s * 0.05, baseY), new Point(m + s * 0.95, baseY));
        }

        /// <summary>İhale Kontrol: gri belge/kağıt + renkli onay işareti (✓)</summary>
        private static void DrawIhaleKontrolIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);

            // Kağıt — gri dikdörtgen
            double pw = s * 0.60;
            double ph = s * 0.80;
            double px = m + s * 0.10;
            double py = m + s * 0.05;
            dc.DrawRoundedRectangle(baseBrush, pen, new Rect(px, py, pw, ph), 2, 2);

            // Metin satırları — gri
            var thinPen = CreateSymbolPen(contourBrush, isLarge ? 1.5 : 1.0);
            double tx1 = px + pw * 0.15;
            double tx2 = px + pw * 0.85;
            dc.DrawLine(thinPen, new Point(tx1, py + ph * 0.22), new Point(tx2, py + ph * 0.22));
            dc.DrawLine(thinPen, new Point(tx1, py + ph * 0.40), new Point(tx2, py + ph * 0.40));
            dc.DrawLine(thinPen, new Point(tx1, py + ph * 0.58), new Point(tx2 * 0.7, py + ph * 0.58));

            // Onay işareti (✓) — renkli, sağ alt köşe
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 3.0 : 2.2);
            double cx = m + s * 0.75;
            double cyy = m + s * 0.72;
            double cr = s * 0.18;

            // Daire arka plan
            dc.DrawEllipse(accentBrush, null, new Point(cx, cyy), cr, cr);

            // Beyaz ✓ işareti
            var whitePen = CreateSymbolPen(Brushes.White, isLarge ? 2.5 : 1.8);
            dc.DrawLine(whitePen, new Point(cx - cr * 0.5, cyy), new Point(cx - cr * 0.1, cyy + cr * 0.4));
            dc.DrawLine(whitePen, new Point(cx - cr * 0.1, cyy + cr * 0.4), new Point(cx + cr * 0.5, cyy - cr * 0.4));
        }

        /// <summary>Enkesit Oku: gri zemin profili + renkli enkesit çizgileri</summary>
        private static void DrawEnkesitOkuIcon(DrawingContext dc, Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            double m = isLarge ? 3 : 1.5;
            double s = size - 2 * m;
            var pen = CreateSymbolPen(contourBrush, isLarge ? 2.5 : 1.8);
            var accentPen = CreateSymbolPen(accentBrush, isLarge ? 2.0 : 1.5);

            // Zemin profili — gri (arazi kesiti şekli)
            var terrain = new StreamGeometry();
            using (var ctx = terrain.Open())
            {
                ctx.BeginFigure(new Point(m + s * 0.05, m + s * 0.55), false, false);
                ctx.LineTo(new Point(m + s * 0.20, m + s * 0.45), true, false);
                ctx.LineTo(new Point(m + s * 0.35, m + s * 0.30), true, false);
                ctx.LineTo(new Point(m + s * 0.50, m + s * 0.25), true, false);
                ctx.LineTo(new Point(m + s * 0.65, m + s * 0.30), true, false);
                ctx.LineTo(new Point(m + s * 0.80, m + s * 0.45), true, false);
                ctx.LineTo(new Point(m + s * 0.95, m + s * 0.55), true, false);
            }
            terrain.Freeze();
            dc.DrawGeometry(null, pen, terrain);

            // Proje kotu profili — renkli (düz çizgi gibi ama hafif eğimli)
            var project = new StreamGeometry();
            using (var ctx = project.Open())
            {
                ctx.BeginFigure(new Point(m + s * 0.05, m + s * 0.50), false, false);
                ctx.LineTo(new Point(m + s * 0.30, m + s * 0.38), true, false);
                ctx.LineTo(new Point(m + s * 0.70, m + s * 0.38), true, false);
                ctx.LineTo(new Point(m + s * 0.95, m + s * 0.50), true, false);
            }
            project.Freeze();
            dc.DrawGeometry(null, accentPen, project);

            // Dolgu alan taraması — renkli yarı saydam
            var colors = IconPalette.GetColors(IconType.EnkesitOku);
            var fillBrush = new SolidColorBrush(Color.FromArgb(0x35, colors.Primary.R, colors.Primary.G, colors.Primary.B));
            fillBrush.Freeze();

            var fill = new StreamGeometry();
            using (var ctx = fill.Open())
            {
                // Proje kotu altı + zemin üstü arası
                ctx.BeginFigure(new Point(m + s * 0.30, m + s * 0.38), true, true);
                ctx.LineTo(new Point(m + s * 0.70, m + s * 0.38), true, false);
                ctx.LineTo(new Point(m + s * 0.65, m + s * 0.30), true, false);
                ctx.LineTo(new Point(m + s * 0.50, m + s * 0.25), true, false);
                ctx.LineTo(new Point(m + s * 0.35, m + s * 0.30), true, false);
            }
            fill.Freeze();
            dc.DrawGeometry(fillBrush, null, fill);

            // Dikey çerçeve çizgileri — gri
            var thinPen = CreateSymbolPen(contourBrush, isLarge ? 1.2 : 0.8);
            dc.DrawLine(thinPen, new Point(m + s * 0.05, m + s * 0.15), new Point(m + s * 0.05, m + s * 0.85));
            dc.DrawLine(thinPen, new Point(m + s * 0.95, m + s * 0.15), new Point(m + s * 0.95, m + s * 0.85));

            // Alt çizgi — gri
            dc.DrawLine(thinPen, new Point(m + s * 0.05, m + s * 0.85), new Point(m + s * 0.95, m + s * 0.85));
        }
    }
}
