using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using Metraj.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Metraj.Commands
{
    public static class RibbonManager
    {
        private static bool _ribbonCreated;

        public static void CreateRibbon()
        {
            if (ComponentManager.Ribbon != null)
                AddRibbonTab();
            else
                AcadApp.Idle += OnAppIdle;
        }

        private static void OnAppIdle(object sender, EventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                AcadApp.Idle -= OnAppIdle;
                AddRibbonTab();
            }
        }

        private static void AddRibbonTab()
        {
            if (_ribbonCreated) return;

            var ribbon = ComponentManager.Ribbon;

            foreach (var existingTab in ribbon.Tabs)
            {
                if (existingTab.Id == "METRAJ_TAB")
                {
                    _ribbonCreated = true;
                    return;
                }
            }

            var tab = new RibbonTab
            {
                Title = "Metraj",
                Id = "METRAJ_TAB"
            };

            ribbon.Tabs.Add(tab);

            AddOlcumPanel(tab);
            AddHesaplamaPanel(tab);
            AddYolMetrajPanel(tab);
            AddAraclarPanel(tab);

            tab.IsActive = true;
            _ribbonCreated = true;

            LoggingService.Info("Ribbon tab 'Metraj' oluşturuldu");
        }

        private static void AddOlcumPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Ölçüm" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Metraj\nPaneli", "M", Color.FromRgb(0x00, 0x78, 0xD4), "METRAJ",
                "Metraj Paneli", "Ana metraj panelini açar/kapatır"));
            source.Items.Add(CreateButton("Uzunluk", "U", Color.FromRgb(0x00, 0xBC, 0xD4), "METRAJUZUNLUK",
                "Hızlı Uzunluk", "Seçili nesnelerin toplam uzunluğunu hesaplar"));
            source.Items.Add(CreateButton("Alan", "A", Color.FromRgb(0x4C, 0xAF, 0x50), "METRAJALAN",
                "Hızlı Alan", "Seçili kapalı nesnelerin alanını hesaplar"));
        }

        private static void AddHesaplamaPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Hesaplama" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Kübaj", "K", Color.FromRgb(0xAB, 0x47, 0xBC), "METRAJ",
                "Kübaj Hesabı", "Hacim hesaplama panelini açar"));
            source.Items.Add(CreateButton("Topla", "T", Color.FromRgb(0xFF, 0xA7, 0x26), "METRAJTOPLA",
                "Metin Toplama", "Seçili text nesnelerindeki sayıları toplar"));
        }

        private static void AddYolMetrajPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Yol Metraj" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Yol\nMetraj", "YM", Color.FromRgb(0xE8, 0x59, 0x3C), "YOLMETRAJ",
                "Yol Metraj", "Yol en kesitlerinden kaz\u0131-dolgu k\u00FCbaj hesab\u0131"));
        }

        private static void AddAraclarPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Araçlar" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Yazı\nYaz", "Y", Color.FromRgb(0x66, 0xBB, 0x6A), "METRAJANNOTASYON",
                "Annotasyon", "Çizime ölçü yazısı ekler"));
            source.Items.Add(CreateButton("Excel", "E", Color.FromRgb(0x21, 0x7A, 0x46), "METRAJEXCEL",
                "Excel Dışa Aktar", "Tüm sonuçları Excel dosyasına aktarır"));
            source.Items.Add(CreateButton("Temizle", "X", Color.FromRgb(0xEF, 0x53, 0x50), "METRAJTEMIZLE",
                "Temizle", "Metraj annotasyonlarını temizler"));
            source.Items.Add(CreateButton("Ayarlar", "S", Color.FromRgb(0x78, 0x78, 0x78), "METRAJAYARLAR",
                "Ayarlar", "Metraj ayarlarını açar"));
        }

        private static RibbonButton CreateButton(string text, string iconLetter, Color iconColor, string command,
            string tooltipTitle, string tooltipDesc)
        {
            var btn = new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                LargeImage = CreateSimpleIcon(iconLetter, iconColor, 32),
                Image = CreateSimpleIcon(iconLetter, iconColor, 16),
                CommandParameter = command,
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = new RibbonToolTip
                {
                    Title = tooltipTitle,
                    Content = tooltipDesc,
                    Command = command,
                    IsHelpEnabled = false
                }
            };

            return btn;
        }

        private static ImageSource CreateSimpleIcon(string letter, Color bgColor, int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(bgColor), null,
                    new Rect(1, 1, size - 2, size - 2), 3, 3);

                var text = new FormattedText(letter,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    size * 0.55, Brushes.White, 1.0);

                dc.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private class RibbonCommandHandler : System.Windows.Input.ICommand
        {
            public bool CanExecute(object parameter) => true;

#pragma warning disable CS0067
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public void Execute(object parameter)
            {
                string cmdParam = null;

                if (parameter is RibbonCommandItem ribbonItem)
                    cmdParam = ribbonItem.CommandParameter?.ToString();

                if (!string.IsNullOrEmpty(cmdParam))
                {
                    var doc = AcadApp.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute(cmdParam + " ", true, false, false);
                }
            }
        }
    }
}
