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

            AddAnaPanelGrubu(tab);
            AddModulPanel(tab);
            AddAraclarPanel(tab);

            tab.IsActive = true;
            _ribbonCreated = true;

            LoggingService.Info("Ribbon tab 'Metraj' oluşturuldu");
        }

        private static void AddAnaPanelGrubu(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Panel" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Metraj\nPaneli", "M", Color.FromRgb(0x00, 0x78, 0xD4), "METRAJ",
                "Metraj Paneli", "T\u00FCm mod\u00FClleri i\u00E7eren ana paneli a\u00E7ar/kapat\u0131r"));
        }

        private static RibbonButton CreateSmallButton(string text, string iconLetter, Color iconColor,
            string command, string tooltipDesc)
        {
            var btn = new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                LargeImage = CreateSimpleIcon(iconLetter, iconColor, 32),
                Image = CreateSimpleIcon(iconLetter, iconColor, 16),
                CommandParameter = command,
                CommandHandler = new RibbonCommandHandler(),
                ToolTip = new RibbonToolTip
                {
                    Title = text,
                    Content = tooltipDesc,
                    Command = command,
                    IsHelpEnabled = false
                }
            };
            return btn;
        }

        private static void AddModulPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Mod\u00FCller" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Uzunluk", "U", Color.FromRgb(0x00, 0xBC, 0xD4), "METRAJUZUNLUKPANEL",
                "Uzunluk", "Uzunluk mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("Alan", "A", Color.FromRgb(0x4C, 0xAF, 0x50), "METRAJALPANEL",
                "Alan", "Alan mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("K\u00FCbaj", "K", Color.FromRgb(0xAB, 0x47, 0xBC), "METRAJKUBAJPANEL",
                "K\u00FCbaj", "K\u00FCbaj mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("En Kesit", "EK", Color.FromRgb(0xFF, 0x98, 0x00), "METRAJENKESITPANEL",
                "En Kesit", "En Kesit mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("Toplama", "T", Color.FromRgb(0xFF, 0xA7, 0x26), "METRAJTOPLAMAPANEL",
                "Toplama", "Toplama mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("Yol\nMetraj", "YM", Color.FromRgb(0xE8, 0x59, 0x3C), "YOLMETRAJPANEL",
                "Yol Metraj", "Yol Metraj mod\u00FCl\u00FCn\u00FC ayr\u0131 pencerede a\u00E7"));
            source.Items.Add(CreateButton("\u0130hale\nKontrol", "\u0130K", Color.FromRgb(0xE5, 0x39, 0x35), "IHALEKONTROLPANEL",
                "\u0130hale Kontrol", "\u0130hale dosyas\u0131 enkesit kontrol mod\u00FCl\u00FC"));
            source.Items.Add(CreateButton("Enkesit\nOku", "EO", Color.FromRgb(0x00, 0x96, 0x88), "YOLENKESITOKU",
                "Enkesit Oku", "İhale DWG'den enkesit okuma ve alan hesabı"));
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
