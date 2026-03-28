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
            { CizgiRolu.Zemin, Color.FromRgb(0x4C, 0xAF, 0x50) },
            { CizgiRolu.SiyirmaTaban, Color.FromRgb(0x21, 0x96, 0xF3) },
            { CizgiRolu.ProjeKotu, Color.FromRgb(0xF4, 0x43, 0x36) },
            { CizgiRolu.UstyapiAltKotu, Color.FromRgb(0xFF, 0x98, 0x00) },
            { CizgiRolu.AsinmaTaban, Color.FromRgb(0x9C, 0x27, 0xB0) },
            { CizgiRolu.BinderTaban, Color.FromRgb(0x00, 0xBC, 0xD4) },
            { CizgiRolu.BitumluTemelTaban, Color.FromRgb(0x79, 0x55, 0x48) },
            { CizgiRolu.PlentmiksTaban, Color.FromRgb(0x60, 0x7D, 0x8B) },
            { CizgiRolu.AltTemelTaban, Color.FromRgb(0xFF, 0xC1, 0x07) },
            { CizgiRolu.KirmatasTaban, Color.FromRgb(0xCD, 0xDC, 0x39) },
            { CizgiRolu.EksenCizgisi, Color.FromRgb(0xFF, 0xFF, 0xFF) },
            { CizgiRolu.HendekCizgisi, Color.FromRgb(0x00, 0x96, 0x88) },
            { CizgiRolu.SevCizgisi, Color.FromRgb(0x8B, 0xC3, 0x4A) },
            { CizgiRolu.BanketCizgisi, Color.FromRgb(0xE9, 0x1E, 0x63) },
            { CizgiRolu.CerceveCizgisi, Color.FromRgb(0x42, 0x42, 0x42) },
            { CizgiRolu.GridCizgisi, Color.FromRgb(0x37, 0x37, 0x37) },
            { CizgiRolu.Tanimsiz, Color.FromRgb(0x75, 0x75, 0x75) },
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

                var color = RolRenkleri.ContainsKey(cizgi.Rol) ? RolRenkleri[cizgi.Rol] : Colors.Gray;
                bool secili = cizgi == _secilenCizgi;
                double kalinlik = secili ? 3 : (cizgi.Rol == CizgiRolu.CerceveCizgisi || cizgi.Rol == CizgiRolu.GridCizgisi ? 0.5 : 1.5);

                var polyline = new WpfPolyline();
                polyline.Stroke = new SolidColorBrush(color);
                polyline.StrokeThickness = kalinlik;

                if (cizgi.Rol == CizgiRolu.Tanimsiz || cizgi.Rol == CizgiRolu.Diger)
                    polyline.StrokeDashArray = new System.Windows.Media.DoubleCollection(new[] { 4.0, 2.0 });

                if (secili)
                    polyline.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = color, BlurRadius = 8, ShadowDepth = 0 };

                foreach (var nokta in cizgi.Noktalar)
                {
                    double cx = (nokta.X - minX) * scale + 10 + _pan.X;
                    double cy = canvasH - ((nokta.Y - minY) * scale + 10) + _pan.Y;
                    polyline.Points.Add(new Point(cx, cy));
                }

                polyline.Tag = cizgi;
                polyline.MouseLeftButtonDown += Polyline_Click;
                polyline.Cursor = Cursors.Hand;

                polyline.ToolTip = $"{cizgi.Rol} | {cizgi.LayerAdi} | Renk:{cizgi.RenkIndex}";

                KesitCanvas.Children.Add(polyline);
            }
        }

        private void Polyline_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is WpfPolyline pl && pl.Tag is CizgiTanimi cizgi)
            {
                _secilenCizgi = cizgi;
                BilgiText.Text = $"{cizgi.Rol} — {cizgi.LayerAdi} — Renk: {cizgi.RenkIndex} — Y: {cizgi.OrtalamaY:F2}";
                CizgiSecildi?.Invoke(this, cizgi);
                Ciz();
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
