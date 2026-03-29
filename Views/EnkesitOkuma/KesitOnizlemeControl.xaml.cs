using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Metraj.Models.YolEnkesit;
using WpfPolyline = System.Windows.Shapes.Polyline;

namespace Metraj.Views.EnkesitOkuma
{
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

        public event EventHandler<CizgiTanimi> CizgiSecildi;

        private static readonly Dictionary<CizgiRolu, Color> RolRenkleri = new Dictionary<CizgiRolu, Color>
        {
            { CizgiRolu.Zemin, Color.FromRgb(0x2E, 0x8B, 0x57) },
            { CizgiRolu.SiyirmaTaban, Color.FromRgb(0x37, 0x8A, 0xDD) },
            { CizgiRolu.ProjeKotu, Color.FromRgb(0xE2, 0x4B, 0x4A) },
            { CizgiRolu.UstyapiAltKotu, Color.FromRgb(0x88, 0x88, 0x88) },
            { CizgiRolu.AsinmaTaban, Color.FromRgb(0xD8, 0x5A, 0x30) },
            { CizgiRolu.BinderTaban, Color.FromRgb(0xEF, 0x9F, 0x27) },
            { CizgiRolu.BitumluTemelTaban, Color.FromRgb(0x7F, 0x77, 0xDD) },
            { CizgiRolu.PlentmiksTaban, Color.FromRgb(0x5D, 0xCA, 0xA5) },
            { CizgiRolu.AltTemelTaban, Color.FromRgb(0x97, 0xC4, 0x59) },
            { CizgiRolu.KirmatasTaban, Color.FromRgb(0xB4, 0xB2, 0xA9) },
            { CizgiRolu.EksenCizgisi, Color.FromRgb(0x99, 0x99, 0x99) },
            { CizgiRolu.HendekCizgisi, Color.FromRgb(0xD8, 0x5A, 0x30) },
            { CizgiRolu.SevCizgisi, Color.FromRgb(0x8B, 0x45, 0x13) },
            { CizgiRolu.BanketCizgisi, Color.FromRgb(0xE9, 0x1E, 0x63) },
            { CizgiRolu.CerceveCizgisi, Color.FromRgb(0x42, 0x42, 0x42) },
            { CizgiRolu.GridCizgisi, Color.FromRgb(0x37, 0x37, 0x37) },
            { CizgiRolu.Tanimsiz, Color.FromRgb(0xAA, 0xAA, 0xAA) },
            { CizgiRolu.Diger, Color.FromRgb(0x61, 0x61, 0x61) },
        };

        public KesitOnizlemeControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Ciz();
        }

        public void CizgileriYukle(List<CizgiTanimi> cizgiler)
        {
            _cizgiler = cizgiler;
            _kesit = null;
            _zoom = 1.0;
            _pan = new Point(0, 0);
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
            Ciz();
        }

        private Brush CizgiRengiBelirle(CizgiTanimi cizgi)
        {
            if (cizgi.Rol != CizgiRolu.Tanimsiz && RolRenkleri.ContainsKey(cizgi.Rol))
            {
                var c = RolRenkleri[cizgi.Rol];
                // Cerceve/Grid: %20-30 opacity
                if (cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi)
                    return new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B));
                return new SolidColorBrush(c);
            }

            return AcadRenkBrush(cizgi.RenkIndex);
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
            if (canvasW < 10 || canvasH < 10) return;

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

            // Kalinlik belirleme
            double kalinlik;
            if (secili) kalinlik = 4;
            else if (cizgi.Rol == CizgiRolu.ProjeKotu) kalinlik = 2.5;
            else if (cerceve) kalinlik = 0.3;
            else kalinlik = 1.2;

            var polyline = new WpfPolyline();
            polyline.Stroke = CizgiRengiBelirle(cizgi);
            polyline.StrokeThickness = kalinlik;
            polyline.StrokeLineJoin = PenLineJoin.Round;
            polyline.StrokeStartLineCap = PenLineCap.Round;
            polyline.StrokeEndLineCap = PenLineCap.Round;
            polyline.Cursor = cerceve ? Cursors.Arrow : Cursors.Hand;

            // Sadece SiyirmaTaban kesikli cizilir
            if (cizgi.Rol == CizgiRolu.SiyirmaTaban)
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
                        (s as WpfPolyline).StrokeThickness = tag?.Rol == CizgiRolu.ProjeKotu ? 2.5 : 1.2;
                };
            }

            polyline.ToolTip = $"{cizgi.Rol} | {cizgi.LayerAdi} | Renk:{cizgi.RenkIndex}";
            KesitCanvas.Children.Add(polyline);

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
                case CizgiRolu.SiyirmaTaban: return "siyirma";
                case CizgiRolu.ProjeKotu: return "proje kotu";
                case CizgiRolu.UstyapiAltKotu: return "ustyapi alt";
                case CizgiRolu.AsinmaTaban: return "asinma";
                case CizgiRolu.BinderTaban: return "binder";
                case CizgiRolu.BitumluTemelTaban: return "bitumlu";
                case CizgiRolu.PlentmiksTaban: return "plentmiks";
                case CizgiRolu.AltTemelTaban: return "alttemel";
                case CizgiRolu.KirmatasTaban: return "kirmatas";
                case CizgiRolu.EksenCizgisi: return "CL";
                case CizgiRolu.HendekCizgisi: return "hendek";
                case CizgiRolu.SevCizgisi: return "sev";
                case CizgiRolu.BanketCizgisi: return "banket";
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
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Cift tiklama: fit-to-view (AutoCAD zoom extents)
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
            Sigdir();
        }
    }
}
