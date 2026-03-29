using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Metraj.Models.YolEnkesit;
using WpfPolyline = System.Windows.Shapes.Polyline;

namespace Metraj.Views.EnkesitOkuma
{
    public enum OnizlemeRenkModu
    {
        OrijinalRenk,
        RolRenk,
        HibritRenk
    }

    public partial class KesitOnizlemeControl : UserControl
    {
        private List<CizgiTanimi> _cizgiler;
        private KesitGrubu _kesit; // Alan bilgisi icin
        private double _zoom = 1.0;
        private Point _pan = new Point(0, 0);
        private Point _sonMousePoz;
        private bool _panAktif;
        private CizgiTanimi _secilenCizgi;
        private DateTime _sonOrtaTiklama = DateTime.MinValue;
        private double _icerikW, _icerikH; // Son cizimde icerik boyutu (canvas px)
        private OnizlemeRenkModu _renkModu = OnizlemeRenkModu.RolRenk;

        public event EventHandler<CizgiTanimi> CizgiSecildi;

        public OnizlemeRenkModu RenkModu
        {
            get => _renkModu;
            set { _renkModu = value; Ciz(); }
        }

        private static readonly Color FallbackRenk = Color.FromRgb(0xAA, 0xAA, 0xAA);

        private static readonly Dictionary<CizgiRolu, Color> RolRenkleri = new Dictionary<CizgiRolu, Color>
        {
            { CizgiRolu.Zemin, Color.FromRgb(0x2E, 0x8B, 0x57) },
            { CizgiRolu.ProjeCizgisi, Color.FromRgb(0xE2, 0x4B, 0x4A) },
            { CizgiRolu.Siyirma, Color.FromRgb(0x37, 0x8A, 0xDD) },
            { CizgiRolu.Yarma, Color.FromRgb(0xE0, 0x7C, 0x24) },
            { CizgiRolu.Dolgu, Color.FromRgb(0x8B, 0x45, 0x13) },
            { CizgiRolu.Asinma, Color.FromRgb(0xD8, 0x5A, 0x30) },
            { CizgiRolu.Binder, Color.FromRgb(0xEF, 0x9F, 0x27) },
            { CizgiRolu.BitumluTemel, Color.FromRgb(0x7F, 0x77, 0xDD) },
            { CizgiRolu.Plentmiks, Color.FromRgb(0x5D, 0xCA, 0xA5) },
            { CizgiRolu.AltTemel, Color.FromRgb(0x97, 0xC4, 0x59) },
            { CizgiRolu.BTYerineKonan, Color.FromRgb(0xE9, 0x1E, 0x63) },
            { CizgiRolu.BTYerineKonmayan, Color.FromRgb(0xF4, 0x8F, 0xB1) },
            { CizgiRolu.CerceveCizgisi, Color.FromRgb(0x42, 0x42, 0x42) },
            { CizgiRolu.GridCizgisi, Color.FromRgb(0x37, 0x37, 0x37) },
            { CizgiRolu.Tanimsiz, Color.FromRgb(0xAA, 0xAA, 0xAA) },
            { CizgiRolu.Diger, Color.FromRgb(0x61, 0x61, 0x61) },
        };

        private bool _cizimBekliyor;

        public KesitOnizlemeControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Ciz();
            // Canvas layout tamamlandiginda bekleyen cizimi yap
            KesitCanvas.SizeChanged += (s, e) =>
            {
                if (_cizimBekliyor && KesitCanvas.ActualWidth > 10 && KesitCanvas.ActualHeight > 10)
                {
                    _cizimBekliyor = false;
                    Ciz();
                }
            };
        }

        public void CizgileriYukle(List<CizgiTanimi> cizgiler)
        {
            _cizgiler = cizgiler;
            _kesit = null;
            _zoom = 1.0;
            _pan = new Point(0, 0);
            _highlightLayer = null;
            Ciz();
        }

        /// <summary>Cizgi listesini guncelle ama zoom/pan/highlight'i koru.</summary>
        public void CizgileriGuncelle(List<CizgiTanimi> cizgiler)
        {
            _cizgiler = cizgiler;
            _kesit = null;
            Ciz();
        }

        /// <summary>KesitGrubu ile yukle — alan bilgisi ve gorsel birlestirme icin.</summary>
        public void CizgileriYukle(KesitGrubu kesit)
        {
            _cizgiler = kesit?.Cizgiler;
            _kesit = kesit;
            _zoom = 1.0;
            _pan = new Point(0, 0);
            Ciz();
        }

        /// <summary>Zoom ve pan sifirla, tum cizgileri ekrana sigdir (AutoCAD zoom extents).</summary>
        public void Sigdir()
        {
            _zoom = 1.0;
            _pan = new Point(0, 0);
            Ciz();
        }

        public void VurgulaCizgi(CizgiTanimi cizgi)
        {
            _secilenCizgi = cizgi;
            _highlightLayer = null;
            Ciz();
        }

        private string _highlightLayer;
        private short _highlightRenk;

        /// <summary>
        /// Belirtilen layer+renk grubunun cizgilerini highlight'la ve bounding box'a zoom yap.
        /// null/bos ile cagrilirsa highlight ve zoom sifirlanir.
        /// </summary>
        public void HighlightLayer(string layerAdi, short renkIndex)
        {
            // Ayni layer'a tekrar tiklandiysa (toggle) → sifirla
            if (_highlightLayer == layerAdi && _highlightRenk == renkIndex)
            {
                _highlightLayer = null;
                _zoom = 1.0;
                _pan = new Point(0, 0);
                Ciz();
                return;
            }

            _highlightLayer = layerAdi;
            _highlightRenk = renkIndex;

            // Highlight'li cizgilerin bounding box'ina zoom
            if (_cizgiler != null && layerAdi != null)
            {
                var hedefNoktalar = _cizgiler
                    .Where(c => c.LayerAdi == layerAdi && c.RenkIndex == renkIndex)
                    .SelectMany(c => c.Noktalar)
                    .ToList();

                if (hedefNoktalar.Count > 0)
                    ZoomToBounds(hedefNoktalar);
                else
                    Ciz();
            }
            else
            {
                Ciz();
            }
        }

        /// <summary>Highlight ve zoom'u sifirla, tum kesiti goster.</summary>
        public void HighlightTemizle()
        {
            _highlightLayer = null;
            _zoom = 1.0;
            _pan = new Point(0, 0);
            Ciz();
        }

        /// <summary>Verilen noktalarin bounding box'ina %120 margin ile zoom yapar.</summary>
        private void ZoomToBounds(List<Autodesk.AutoCAD.Geometry.Point2d> noktalar)
        {
            if (noktalar.Count == 0) { Ciz(); return; }

            double canvasW = KesitCanvas.ActualWidth;
            double canvasH = KesitCanvas.ActualHeight;
            if (canvasW < 10 || canvasH < 10) { Ciz(); return; }

            // Tum cizgilerin genel fit bounds'u (Ciz() ile ayni mantik)
            var anlamliCizgiler = _cizgiler.Where(c =>
                c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi).ToList();
            var fitNoktalar = (anlamliCizgiler.Count > 0 ? anlamliCizgiler : _cizgiler)
                .SelectMany(c => c.Noktalar).ToList();
            if (fitNoktalar.Count == 0) { Ciz(); return; }

            double globalMinX = fitNoktalar.Min(p => p.X);
            double globalMaxX = fitNoktalar.Max(p => p.X);
            var yDeg = fitNoktalar.Select(p => p.Y).OrderBy(y => y).ToList();
            int tA = (int)(yDeg.Count * 0.05);
            int tU = Math.Min((int)(yDeg.Count * 0.95), yDeg.Count - 1);
            if (tA >= tU) { tA = 0; tU = yDeg.Count - 1; }
            double globalMinY = yDeg[tA] - (yDeg[tU] - yDeg[tA]) * 0.10;
            double globalMaxY = yDeg[tU] + (yDeg[tU] - yDeg[tA]) * 0.10;
            double globalRangeX = Math.Max(globalMaxX - globalMinX, 0.01);
            double globalRangeY = Math.Max(globalMaxY - globalMinY, 0.01);

            // Hedef bounds (%120 margin)
            double tMinX = noktalar.Min(p => p.X);
            double tMaxX = noktalar.Max(p => p.X);
            double tMinY = noktalar.Min(p => p.Y);
            double tMaxY = noktalar.Max(p => p.Y);
            double marjX = Math.Max((tMaxX - tMinX) * 0.10, globalRangeX * 0.02);
            double marjY = Math.Max((tMaxY - tMinY) * 0.10, globalRangeY * 0.02);
            tMinX -= marjX; tMaxX += marjX;
            tMinY -= marjY; tMaxY += marjY;
            double tRangeX = Math.Max(tMaxX - tMinX, 0.01);
            double tRangeY = Math.Max(tMaxY - tMinY, 0.01);

            // Zoom: hedef bounds'u canvas'a sigdirmak icin gereken zoom orani
            double padding = 30;
            double baseScaleX = (canvasW - padding * 2) / globalRangeX;
            double baseScaleY = (canvasH - padding * 2) / globalRangeY;
            double baseScale = Math.Min(baseScaleX, baseScaleY);

            double neededScaleX = (canvasW - padding * 2) / tRangeX;
            double neededScaleY = (canvasH - padding * 2) / tRangeY;
            double neededScale = Math.Min(neededScaleX, neededScaleY);

            _zoom = Math.Max(1.0, neededScale / baseScale);

            // Pan: hedef merkezi canvas merkezine getir
            double scale = baseScale * _zoom;
            double tCenterX = (tMinX + tMaxX) / 2.0;
            double tCenterY = (tMinY + tMaxY) / 2.0;
            double canvasCenterX = canvasW / 2.0;
            double canvasCenterY = canvasH / 2.0;
            double drawCenterX = (tCenterX - globalMinX) * scale + padding;
            double drawCenterY = canvasH - ((tCenterY - globalMinY) * scale + padding);
            _pan = new Point(canvasCenterX - drawCenterX, canvasCenterY - drawCenterY);

            Ciz();
        }

        private Brush CizgiRengiBelirle(CizgiTanimi cizgi)
        {
            Color renk;

            if (_renkModu == OnizlemeRenkModu.OrijinalRenk)
            {
                renk = AcadRenkCevir(cizgi.RenkIndex);
            }
            else if (_renkModu == OnizlemeRenkModu.HibritRenk)
            {
                // Rol atanmis → rol rengi, atanmamis → orijinal AutoCAD rengi
                bool rolAtanmis = cizgi.Rol != CizgiRolu.Tanimsiz && cizgi.Rol != CizgiRolu.Diger;
                renk = rolAtanmis && RolRenkleri.TryGetValue(cizgi.Rol, out var rr)
                    ? rr
                    : AcadRenkCevir(cizgi.RenkIndex);
            }
            else
            {
                renk = RolRenkleri.TryGetValue(cizgi.Rol, out var rr) ? rr : FallbackRenk;
            }

            // Cerceve/Grid: soluk goster
            if (cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi)
                return new SolidColorBrush(Color.FromArgb(0x40, renk.R, renk.G, renk.B));

            return new SolidColorBrush(renk);
        }

        /// <summary>AutoCAD ACI renk index'ini WPF Color'a cevirir.</summary>
        private static Color AcadRenkCevir(short colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return Color.FromRgb(0xFF, 0x00, 0x00); // Kirmizi
                case 2: return Color.FromRgb(0xFF, 0xFF, 0x00); // Sari
                case 3: return Color.FromRgb(0x00, 0xFF, 0x00); // Yesil
                case 4: return Color.FromRgb(0x00, 0xFF, 0xFF); // Cyan
                case 5: return Color.FromRgb(0x00, 0x00, 0xFF); // Mavi
                case 6: return Color.FromRgb(0xFF, 0x00, 0xFF); // Magenta
                case 7: return Color.FromRgb(0xFF, 0xFF, 0xFF); // Beyaz
                case 8: return Color.FromRgb(0x80, 0x80, 0x80); // Gri
                case 9: return Color.FromRgb(0xC0, 0xC0, 0xC0); // Acik gri
                default: return Color.FromRgb(0xAA, 0xAA, 0xAA); // Fallback
            }
        }

        private Brush AcadRenkBrush(short colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return new SolidColorBrush(Color.FromRgb(0xE2, 0x4B, 0x4A));
                case 2: return new SolidColorBrush(Color.FromRgb(0xEF, 0xB8, 0x4E));
                case 3: return new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
                case 4: return new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
                case 5: return new SolidColorBrush(Color.FromRgb(0x37, 0x8A, 0xDD));
                case 6: return new SolidColorBrush(Color.FromRgb(0xC5, 0x60, 0xD0));
                case 7: return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                default:
                    try
                    {
                        var acadColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
                        return new SolidColorBrush(Color.FromRgb(acadColor.Red, acadColor.Green, acadColor.Blue));
                    }
                    catch
                    {
                        return new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
                    }
            }
        }

        private void Ciz()
        {
            KesitCanvas.Children.Clear();
            if (_cizgiler == null || _cizgiler.Count == 0) return;

            double canvasW = KesitCanvas.ActualWidth;
            double canvasH = KesitCanvas.ActualHeight;
            if (canvasW < 10 || canvasH < 10)
            {
                _cizimBekliyor = true;
                // SizeChanged tetiklenmezse diye Dispatcher ile tekrar dene
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (_cizimBekliyor && KesitCanvas.ActualWidth > 10 && KesitCanvas.ActualHeight > 10)
                    {
                        _cizimBekliyor = false;
                        Ciz();
                    }
                }));
                return;
            }

            // Fit-to-view: cerceve/grid HARIC, diger tum cizgilerle fit hesapla
            var anlamliCizgiler = _cizgiler.Where(c =>
                c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi).ToList();

            var fitNoktalar = (anlamliCizgiler.Count > 0 ? anlamliCizgiler : _cizgiler)
                .SelectMany(c => c.Noktalar).ToList();

            if (fitNoktalar.Count == 0) return;

            double minX = fitNoktalar.Min(p => p.X);
            double maxX = fitNoktalar.Max(p => p.X);

            // Trimmed fit: Y ekseninde outlier'lari cikar (%5 alt, %5 ust)
            // Hendek gibi asiri derin yapilar fit hesabini bozmasin
            var yDegerleri = fitNoktalar.Select(p => p.Y).OrderBy(y => y).ToList();
            int trimAlt = (int)(yDegerleri.Count * 0.05);
            int trimUst = Math.Min((int)(yDegerleri.Count * 0.95), yDegerleri.Count - 1);
            if (trimAlt >= trimUst) { trimAlt = 0; trimUst = yDegerleri.Count - 1; }

            double minY = yDegerleri[trimAlt];
            double maxY = yDegerleri[trimUst];

            // Trim sonrasi biraz marj ekle (%10) - cizgiler kesilmesin
            double yMarj = (maxY - minY) * 0.10;
            minY -= yMarj;
            maxY += yMarj;

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;
            if (rangeX < 0.01) rangeX = 1;
            if (rangeY < 0.01) rangeY = 1;

            double padding = 30;
            double scaleX = (canvasW - padding * 2) / rangeX * _zoom;
            double scaleY = (canvasH - padding * 2) / rangeY * _zoom;
            double scale = Math.Min(scaleX, scaleY);

            // Icerik boyutunu sakla — pan siniri icin
            _icerikW = rangeX * scale;
            _icerikH = rangeY * scale;

            // Cerceve/grid cizgilerini ONCE ciz (arkada kalsin)
            foreach (var cizgi in _cizgiler.Where(c => c.Rol == CizgiRolu.CerceveCizgisi || c.Rol == CizgiRolu.GridCizgisi))
                CizgiCiz(cizgi, scale, minX, minY, canvasH, padding);

            // Diger tum cizgileri ustune ciz (eskisi gibi — hesaptan bagimsiz)
            foreach (var cizgi in _cizgiler.Where(c =>
                c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi))
            {
                CizgiCiz(cizgi, scale, minX, minY, canvasH, padding);
            }
        }

        private void CizgiCiz(CizgiTanimi cizgi, double scale, double minX, double minY, double canvasH, double padding)
        {
            if (cizgi.Noktalar.Count < 2) return;

            bool secili = cizgi == _secilenCizgi;
            bool cerceve = cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi;
            bool highlighted = _highlightLayer != null && cizgi.LayerAdi == _highlightLayer && cizgi.RenkIndex == _highlightRenk;
            bool highlightAktif = _highlightLayer != null;
            bool soluk = highlightAktif && !highlighted && !secili;

            // Kalinlik belirleme
            double kalinlik;
            if (secili) kalinlik = 4;
            else if (highlighted) kalinlik = 3.5;
            else if (cizgi.Rol == CizgiRolu.ProjeCizgisi && !soluk) kalinlik = 2.5;
            else if (cerceve) kalinlik = 0.3;
            else if (soluk) kalinlik = 0.6;
            else kalinlik = 1.2;

            var polyline = new WpfPolyline();
            var stroke = CizgiRengiBelirle(cizgi);
            if (soluk && stroke is SolidColorBrush scb)
                stroke = new SolidColorBrush(Color.FromArgb(0x33, scb.Color.R, scb.Color.G, scb.Color.B));
            polyline.Stroke = stroke;
            polyline.StrokeThickness = kalinlik;
            polyline.StrokeLineJoin = PenLineJoin.Round;
            polyline.StrokeStartLineCap = PenLineCap.Round;
            polyline.StrokeEndLineCap = PenLineCap.Round;
            polyline.Cursor = cerceve ? Cursors.Arrow : Cursors.Hand;

            // Sadece SiyirmaTaban kesikli cizilir
            if (cizgi.Rol == CizgiRolu.Siyirma)
                polyline.StrokeDashArray = new System.Windows.Media.DoubleCollection(new[] { 6.0, 3.0 });

            if (secili)
                polyline.Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.White, BlurRadius = 10, ShadowDepth = 0 };

            foreach (var nokta in cizgi.Noktalar)
            {
                double cx = (nokta.X - minX) * scale + padding + _pan.X;
                double cy = canvasH - ((nokta.Y - minY) * scale + padding) + _pan.Y;
                polyline.Points.Add(new Point(cx, cy));
            }

            polyline.Tag = cizgi;
            if (!cerceve)
            {
                polyline.MouseLeftButtonDown += Polyline_Click;
                polyline.MouseEnter += (s, ev) =>
                {
                    if ((s as WpfPolyline)?.Tag != _secilenCizgi)
                        (s as WpfPolyline).StrokeThickness = 3;
                };
                polyline.MouseLeave += (s, ev) =>
                {
                    var tag = (s as WpfPolyline)?.Tag as CizgiTanimi;
                    if (tag != _secilenCizgi)
                        (s as WpfPolyline).StrokeThickness = tag?.Rol == CizgiRolu.ProjeCizgisi ? 2.5 : 1.2;
                };
            }

            polyline.ToolTip = $"{cizgi.Rol} | {cizgi.LayerAdi} | Renk:{cizgi.RenkIndex}";
            KesitCanvas.Children.Add(polyline);

            // Kapali entity'ler icin yari-seffaf dolgu polygon
            bool malzemeRolu = cizgi.Rol == CizgiRolu.Siyirma || cizgi.Rol == CizgiRolu.Yarma
                || cizgi.Rol == CizgiRolu.Dolgu || cizgi.Rol == CizgiRolu.Asinma
                || cizgi.Rol == CizgiRolu.Binder || cizgi.Rol == CizgiRolu.BitumluTemel
                || cizgi.Rol == CizgiRolu.Plentmiks || cizgi.Rol == CizgiRolu.AltTemel
                || cizgi.Rol == CizgiRolu.BTYerineKonan || cizgi.Rol == CizgiRolu.BTYerineKonmayan;
            if (cizgi.KapaliMi && malzemeRolu && polyline.Points.Count >= 3)
            {
                var dolguRenk = (CizgiRengiBelirle(cizgi) as SolidColorBrush)?.Color ?? FallbackRenk;
                byte dolguAlpha = soluk ? (byte)0x10 : (byte)0x30;
                var polygon = new System.Windows.Shapes.Polygon
                {
                    Fill = new SolidColorBrush(Color.FromArgb(dolguAlpha, dolguRenk.R, dolguRenk.G, dolguRenk.B)),
                    Stroke = null,
                    IsHitTestVisible = false
                };
                foreach (var pt in polyline.Points)
                    polygon.Points.Add(pt);
                KesitCanvas.Children.Add(polygon);
            }

            // Cizgi etiketi (ana roller icin, cerceve/grid haric)
            if (!cerceve && cizgi.Rol != CizgiRolu.Tanimsiz && cizgi.Rol != CizgiRolu.Diger
                && cizgi.Noktalar.Count > 0)
            {
                var sonNokta = cizgi.Noktalar.Last();
                double lx = (sonNokta.X - minX) * scale + padding + 6 + _pan.X;
                double ly = canvasH - ((sonNokta.Y - minY) * scale + padding) + _pan.Y;

                string durum = cizgi.Rol != CizgiRolu.Tanimsiz ? " \u2713" : " ?";
                var etiket = new TextBlock
                {
                    Text = RolKisaAd(cizgi.Rol) + durum,
                    FontSize = 10,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = CizgiRengiBelirle(cizgi),
                };
                Canvas.SetLeft(etiket, lx);
                Canvas.SetTop(etiket, ly - 8);
                KesitCanvas.Children.Add(etiket);
            }
        }

        private string RolKisaAd(CizgiRolu rol)
        {
            switch (rol)
            {
                case CizgiRolu.Zemin: return "zemin";
                case CizgiRolu.ProjeCizgisi: return "proje çizgisi";
                case CizgiRolu.Siyirma: return "siyirma";
                case CizgiRolu.Yarma: return "yarma";
                case CizgiRolu.Dolgu: return "dolgu";
                case CizgiRolu.Asinma: return "asinma";
                case CizgiRolu.Binder: return "binder";
                case CizgiRolu.BitumluTemel: return "bitumlu";
                case CizgiRolu.Plentmiks: return "plentmiks";
                case CizgiRolu.AltTemel: return "alttemel";
                case CizgiRolu.BTYerineKonan: return "BT konan";
                case CizgiRolu.BTYerineKonmayan: return "BT konmayan";
                default: return rol.ToString();
            }
        }

        private void Polyline_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is WpfPolyline pl && pl.Tag is CizgiTanimi cizgi)
            {
                _secilenCizgi = cizgi;
                BilgiText.Text = CizgiBilgiMetniOlustur(cizgi);
                CizgiSecildi?.Invoke(this, cizgi);
                Ciz();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Tiklanan cizgi icin bilgi metni olusturur.
        /// Rol atanmissa ilgili malzemelerin alan degerlerini gosterir.
        /// </summary>
        private string CizgiBilgiMetniOlustur(CizgiTanimi cizgi)
        {
            string temel = $"{cizgi.Rol} -- {cizgi.LayerAdi}";

            // Alan bilgisi: bu rol hangi malzemelerde kullaniliyor?
            if (_kesit?.HesaplananAlanlar != null && cizgi.Rol != CizgiRolu.Tanimsiz
                && cizgi.Rol != CizgiRolu.CerceveCizgisi && cizgi.Rol != CizgiRolu.GridCizgisi)
            {
                var ilgiliAlanlar = _kesit.HesaplananAlanlar
                    .Where(a => a.UstCizgiRolu == cizgi.Rol || a.AltCizgiRolu == cizgi.Rol)
                    .ToList();

                if (ilgiliAlanlar.Count > 0)
                {
                    var alanBilgi = string.Join(", ", ilgiliAlanlar.Select(a => $"{a.MalzemeAdi}: {a.Alan:F2} m\u00B2"));
                    return $"{temel} -- {alanBilgi}";
                }
            }

            return $"{temel} -- Renk: {cizgi.RenkIndex}";
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double eskiZoom = _zoom;
            double factor = e.Delta > 0 ? 1.2 : 0.83;
            _zoom *= factor;
            _zoom = Math.Max(0.01, Math.Min(500, _zoom));

            // Mouse pozisyonuna gore zoom — imlec noktasi sabit kalsin
            var mousePos = e.GetPosition(KesitCanvas);
            double oranDeg = _zoom / eskiZoom;
            _pan = new Point(
                mousePos.X - (mousePos.X - _pan.X) * oranDeg,
                mousePos.Y - (mousePos.Y - _pan.Y) * oranDeg);

            Ciz();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Sol cift tiklama: highlight temizle + fit-to-view
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                HighlightTemizle();
                e.Handled = true;
                return;
            }

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Orta cift tiklama: fit-to-view (AutoCAD zoom extents)
                var simdi = DateTime.Now;
                if ((simdi - _sonOrtaTiklama).TotalMilliseconds < 400)
                {
                    Sigdir();
                    _sonOrtaTiklama = DateTime.MinValue;
                    e.Handled = true;
                    return;
                }
                _sonOrtaTiklama = simdi;

                _panAktif = true;
                _sonMousePoz = e.GetPosition(KesitCanvas);
                KesitCanvas.CaptureMouse();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released && _panAktif)
            {
                _panAktif = false;
                KesitCanvas.ReleaseMouseCapture();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_panAktif) return;
            var pos = e.GetPosition(KesitCanvas);
            _pan = new Point(_pan.X + pos.X - _sonMousePoz.X, _pan.Y + pos.Y - _sonMousePoz.Y);
            _sonMousePoz = pos;
            Ciz();
        }

        /// <summary>Pan degerini sinirla — cizim tamamen ekran disina kaymasin.</summary>
        private void PanSinirla()
        {
            double canvasW = KesitCanvas.ActualWidth;
            double canvasH = KesitCanvas.ActualHeight;
            if (canvasW < 10 || canvasH < 10 || _icerikW < 1) return;

            double padding = 30;
            // Izin verilen aralik: icerik en az %20 gorunur kalsin
            double marjX = _icerikW * 0.8;
            double marjY = _icerikH * 0.8;

            double minPanX = -(padding + marjX);
            double maxPanX = canvasW - padding - _icerikW + marjX;
            double minPanY = -(padding + marjY);
            double maxPanY = canvasH - padding - _icerikH + marjY;

            // min > max olabilir (icerik ekrandan buyukse), swap
            if (minPanX > maxPanX) { double t = minPanX; minPanX = maxPanX; maxPanX = t; }
            if (minPanY > maxPanY) { double t = minPanY; minPanY = maxPanY; maxPanY = t; }

            _pan = new Point(
                Math.Max(minPanX, Math.Min(maxPanX, _pan.X)),
                Math.Max(minPanY, Math.Min(maxPanY, _pan.Y)));
        }

        private void BtnSigdir_Click(object sender, RoutedEventArgs e)
        {
            HighlightTemizle();
        }
    }
}
