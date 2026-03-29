using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Metraj.Commands
{
    public enum IconType
    {
        MetrajPanel,
        Uzunluk,
        Alan,
        Toplama,
        YolMetraj,
        EnkesitOku
    }

    public struct ModuleColorSet
    {
        public readonly Color Light;
        public readonly Color Primary;
        public readonly Color Dark;

        public ModuleColorSet(Color light, Color primary, Color dark)
        {
            Light = light;
            Primary = primary;
            Dark = dark;
        }
    }

    public static class IconPalette
    {
        // Mavi — Ana panel
        public static readonly ModuleColorSet Mavi = new ModuleColorSet(
            Color.FromRgb(0x6E, 0xC6, 0xFF), Color.FromRgb(0x4F, 0xB3, 0xF7), Color.FromRgb(0x29, 0x7A, 0xCC));

        // Cyan — Uzunluk
        public static readonly ModuleColorSet Cyan = new ModuleColorSet(
            Color.FromRgb(0x80, 0xDE, 0xEA), Color.FromRgb(0x00, 0xBC, 0xD4), Color.FromRgb(0x00, 0x83, 0x8F));

        // Yeşil — Alan
        public static readonly ModuleColorSet Yesil = new ModuleColorSet(
            Color.FromRgb(0x81, 0xD4, 0x8A), Color.FromRgb(0x4C, 0xAF, 0x50), Color.FromRgb(0x2E, 0x7D, 0x32));

        // Turuncu — Toplama
        public static readonly ModuleColorSet Turuncu = new ModuleColorSet(
            Color.FromRgb(0xFF, 0xCC, 0x5C), Color.FromRgb(0xFF, 0xA7, 0x26), Color.FromRgb(0xCC, 0x7A, 0x00));

        // Kırmızı — Yol Metraj
        public static readonly ModuleColorSet Kirmizi = new ModuleColorSet(
            Color.FromRgb(0xFF, 0x8A, 0x65), Color.FromRgb(0xE8, 0x59, 0x3C), Color.FromRgb(0xBF, 0x36, 0x0C));

        // Teal — Enkesit Oku
        public static readonly ModuleColorSet Teal = new ModuleColorSet(
            Color.FromRgb(0x80, 0xCB, 0xC4), Color.FromRgb(0x00, 0x96, 0x88), Color.FromRgb(0x00, 0x69, 0x5C));

        public static ModuleColorSet GetColors(IconType type)
        {
            switch (type)
            {
                case IconType.MetrajPanel:  return Mavi;
                case IconType.Uzunluk:      return Cyan;
                case IconType.Alan:         return Yesil;
                case IconType.Toplama:      return Turuncu;
                case IconType.YolMetraj:    return Kirmizi;
                case IconType.EnkesitOku:   return Teal;
                default:                    return Mavi;
            }
        }
    }

    public static partial class RibbonIconFactory
    {
        // Civil 3D tarzı: gri gövde + renkli aksan detayları
        private static readonly SolidColorBrush BaseBrush;
        private static readonly SolidColorBrush ContourBrush;

        static RibbonIconFactory()
        {
            BaseBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD5));
            BaseBrush.Freeze();
            ContourBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x95, 0xA5));
            ContourBrush.Freeze();
        }

        public static BitmapSource CreateIcon(IconType type, int size)
        {
            var colors = IconPalette.GetColors(type);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                bool isLarge = size >= 32;
                var accentBrush = new SolidColorBrush(colors.Primary);
                accentBrush.Freeze();

                DrawSymbol(dc, type, BaseBrush, ContourBrush, accentBrush, size, isLarge);
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawSymbol(DrawingContext dc, IconType type,
            Brush baseBrush, Brush contourBrush, Brush accentBrush, int size, bool isLarge)
        {
            switch (type)
            {
                case IconType.MetrajPanel:  DrawMetrajPanelIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
                case IconType.Uzunluk:      DrawUzunlukIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
                case IconType.Alan:         DrawAlanIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
                case IconType.Toplama:      DrawToplamaIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
                case IconType.YolMetraj:    DrawYolMetrajIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
                case IconType.EnkesitOku:   DrawEnkesitOkuIcon(dc, baseBrush, contourBrush, accentBrush, size, isLarge); break;
            }
        }

        private static Pen CreateSymbolPen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }
    }
}
