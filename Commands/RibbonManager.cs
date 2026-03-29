using System;
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

            tab.IsActive = true;
            _ribbonCreated = true;

            LoggingService.Info("Ribbon tab 'Metraj' oluşturuldu");
        }

        private static void AddAnaPanelGrubu(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Panel" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Metraj\nPaneli", IconType.MetrajPanel, "METRAJ",
                "Metraj Paneli", "Tüm modülleri içeren ana paneli açar/kapatır"));
        }

        private static void AddModulPanel(RibbonTab tab)
        {
            var source = new RibbonPanelSource { Title = "Modüller" };
            var panel = new RibbonPanel { Source = source };
            tab.Panels.Add(panel);

            source.Items.Add(CreateButton("Uzunluk", IconType.Uzunluk, "METRAJUZUNLUKPANEL",
                "Uzunluk", "Uzunluk modülünü ayrı pencerede aç"));
            source.Items.Add(CreateButton("Alan", IconType.Alan, "METRAJALPANEL",
                "Alan", "Alan modülünü ayrı pencerede aç"));
            source.Items.Add(CreateButton("Toplama", IconType.Toplama, "METRAJTOPLAMAPANEL",
                "Toplama", "Toplama modülünü ayrı pencerede aç"));
            source.Items.Add(CreateButton("Yol\nMetraj", IconType.YolMetraj, "YOLMETRAJPANEL",
                "Yol Metraj", "Yol Metraj modülünü ayrı pencerede aç"));
            source.Items.Add(CreateButton("İhale\nKontrol", IconType.IhaleKontrol, "IHALEKONTROLPANEL",
                "İhale Kontrol", "İhale dosyası enkesit kontrol modülü"));
            source.Items.Add(CreateButton("Enkesit\nOku", IconType.EnkesitOku, "YOLENKESITOKU",
                "Enkesit Oku", "İhale DWG'den enkesit okuma ve alan hesabı"));
        }

        private static RibbonButton CreateButton(string text, IconType iconType, string command,
            string tooltipTitle, string tooltipDesc)
        {
            var btn = new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                LargeImage = RibbonIconFactory.CreateIcon(iconType, 32),
                Image = RibbonIconFactory.CreateIcon(iconType, 16),
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
