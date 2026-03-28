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
        private double _zoom = 1.0;
        private Point _pan = new Point(0, 0);
        private Point _sonMousePoz;
        private bool _panAktif;
        private CizgiTanimi _secilenCizgi;

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
                if (cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi)
                    return new SolidColorBrush(Color.FromArgb(0x55, c.R, c.G, c.B));
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

            var tumNoktalar = _cizgiler.SelectMany(c => c.Noktalar).ToList();
            if (tumNoktalar.Count == 0) return;

            double minX = tumNoktalar.Min(p => p.X);
            double maxX = tumNoktalar.Max(p => p.X);
            double minY = tumNoktalar.Min(p => p.Y);
            double maxY = tumNoktalar.Max(p => p.Y);

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;
            if (rangeX < 0.01) rangeX = 1;
            if (rangeY < 0.01) rangeY = 1;

            double scaleX = (canvasW - 20) / rangeX * _zoom;
            double scaleY = (canvasH - 20) / rangeY * _zoom;
            double scale = Math.Min(scaleX, scaleY);

            foreach (var cizgi in _cizgiler)
            {
                if (cizgi.Noktalar.Count < 2) continue;

                bool secili = cizgi == _secilenCizgi;
                double kalinlik = secili ? 4 : (cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi ? 0.5 : 1.5);

                var polyline = new WpfPolyline();
                polyline.Stroke = CizgiRengiBelirle(cizgi);
                polyline.StrokeThickness = kalinlik;
                polyline.StrokeLineJoin = PenLineJoin.Round;
                polyline.StrokeStartLineCap = PenLineCap.Round;
                polyline.StrokeEndLineCap = PenLineCap.Round;
                polyline.Cursor = Cursors.Hand;

                if (cizgi.Rol == CizgiRolu.Tanimsiz || cizgi.Rol == CizgiRolu.Diger)
                    polyline.StrokeDashArray = new System.Windows.Media.DoubleCollection(new[] { 4.0, 2.0 });

                if (secili)
                    polyline.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = Colors.White, BlurRadius = 10, ShadowDepth = 0 };

                foreach (var nokta in cizgi.Noktalar)
                {
                    double cx = (nokta.X - minX) * scale + 10 + _pan.X;
                    double cy = canvasH - ((nokta.Y - minY) * scale + 10) + _pan.Y;
                    polyline.Points.Add(new Point(cx, cy));
                }

                polyline.Tag = cizgi;
                polyline.MouseLeftButtonDown += Polyline_Click;

                polyline.MouseEnter += (s, ev) =>
                {
                    if ((s as WpfPolyline)?.Tag != _secilenCizgi)
                        (s as WpfPolyline).StrokeThickness = 3;
                };
                polyline.MouseLeave += (s, ev) =>
                {
                    if ((s as WpfPolyline)?.Tag != _secilenCizgi)
                        (s as WpfPolyline).StrokeThickness = 1.5;
                };

                polyline.ToolTip = $"{cizgi.Rol} | {cizgi.LayerAdi} | Renk:{cizgi.RenkIndex}";

                KesitCanvas.Children.Add(polyline);

                // Cizgi etiketi ekle (ana roller icin)
                if (cizgi.Rol != CizgiRolu.Tanimsiz && cizgi.Rol != CizgiRolu.Diger
                    && cizgi.Rol != CizgiRolu.CerceveCizgisi && cizgi.Rol != CizgiRolu.GridCizgisi
                    && cizgi.Noktalar.Count > 0)
                {
                    var sonNokta = cizgi.Noktalar.Last();
                    double lx = (sonNokta.X - minX) * scale + 14 + _pan.X;
                    double ly = canvasH - ((sonNokta.Y - minY) * scale + 10) + _pan.Y;

                    string etiketAdi = RolKisaAd(cizgi.Rol);
                    var etiket = new System.Windows.Controls.TextBlock
                    {
                        Text = etiketAdi,
                        FontSize = 10,
                        FontFamily = new FontFamily("Segoe UI"),
                        Foreground = CizgiRengiBelirle(cizgi),
                    };
                    Canvas.SetLeft(etiket, lx);
                    Canvas.SetTop(etiket, ly - 8);
                    KesitCanvas.Children.Add(etiket);
                }
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
                BilgiText.Text = $"{cizgi.Rol} -- {cizgi.LayerAdi} -- Renk: {cizgi.RenkIndex} -- Y: {cizgi.OrtalamaY:F2}";
                CizgiSecildi?.Invoke(this, cizgi);
                Ciz();
                e.Handled = true;
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 0.87;
            _zoom *= factor;
            _zoom = Math.Max(0.1, Math.Min(50, _zoom));
            Ciz();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
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
    }
}
