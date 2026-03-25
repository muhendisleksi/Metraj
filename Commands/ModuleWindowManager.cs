using System;
using System.Windows;
using System.Windows.Interop;
using Metraj.Infrastructure;
using Metraj.Services;
using Metraj.ViewModels;
using Metraj.Views;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Metraj.Commands
{
    public class ModuleWindowManager : IDisposable
    {
        private Window _mainWindow;
        private MetrajMainControl _mainControl;
        private bool _disposed;

        public void Toggle()
        {
            if (_mainWindow != null)
            {
                if (_mainWindow.IsVisible)
                    _mainWindow.Hide();
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
                return;
            }

            // İlk açılış — pencereyi oluştur
            _mainControl = new MetrajMainControl();

            // ViewModel'leri bağla
            var mainVm = ServiceContainer.GetRequiredService<MainViewModel>();
            _mainControl.DataContext = mainVm;

            // Tab'lara ViewModel ata
            _mainControl.UzunlukTab.DataContext = ServiceContainer.GetRequiredService<UzunlukViewModel>();
            _mainControl.AlanTab.DataContext = ServiceContainer.GetRequiredService<AlanViewModel>();
            _mainControl.HacimTab.DataContext = ServiceContainer.GetRequiredService<HacimViewModel>();
            _mainControl.ToplamaTab.DataContext = ServiceContainer.GetRequiredService<ToplamaViewModel>();
            _mainControl.EnKesitTab.DataContext = ServiceContainer.GetRequiredService<EnKesitAlanViewModel>();
            _mainControl.AyarlarTab.DataContext = ServiceContainer.GetRequiredService<AyarlarViewModel>();

            _mainWindow = new Window
            {
                Title = "Metraj Asistani",
                Content = _mainControl,
                Width = 420,
                Height = 650,
                MinWidth = 300,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            };

            // AutoCAD'in child'i yap — arkaya gitmesin
            SetAcadOwner(_mainWindow);

            // Kapama yerine gizle
            _mainWindow.Closing += (s, e) =>
            {
                if (!_disposed)
                {
                    e.Cancel = true;
                    _mainWindow.Hide();
                }
            };

            _mainWindow.Show();
            LoggingService.Info("Metraj paneli acildi");
        }

        public void Close()
        {
            _mainWindow?.Hide();
        }

        private static void SetAcadOwner(Window window)
        {
            try
            {
                var acadWindow = AcadApp.MainWindow;
                if (acadWindow != null && acadWindow.Handle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(window);
                    helper.Owner = acadWindow.Handle;
                }
            }
            catch
            {
                // AutoCAD penceresi bulunamazsa devam et
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_mainWindow != null)
            {
                _mainWindow.Closing -= null;
                _mainWindow.Close();
                _mainWindow = null;
            }
        }
    }
}
