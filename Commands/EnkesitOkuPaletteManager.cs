using System;
using System.Windows;
using System.Windows.Interop;
using Metraj.Infrastructure;
using Metraj.Services;
using Metraj.ViewModels.EnkesitOkuma;
using Metraj.Views.EnkesitOkuma;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Metraj.Commands
{
    public class EnkesitOkuPaletteManager : IDisposable
    {
        private Window _window;
        private EnkesitOkuMainControl _control;
        private bool _disposed;

        public void Toggle()
        {
            if (_window != null)
            {
                if (_window.IsVisible)
                    _window.Hide();
                else
                {
                    _window.Show();
                    _window.Activate();
                }
                return;
            }

            _control = new EnkesitOkuMainControl();
            var vm = ServiceContainer.GetRequiredService<EnkesitOkumaMainViewModel>();
            _control.DataContext = vm;

            // Secim sirasinda paneli gizle/goster
            vm.PanelGizle += () => _window?.Hide();
            vm.PanelGoster += () => { _window?.Show(); _window?.Activate(); };

            _window = new Window
            {
                Title = "Yol Enkesit Okuma",
                Content = _control,
                Width = 420,
                Height = 600,
                MinWidth = 360,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true,
                Topmost = false
            };

            var acadHandle = AcadApp.MainWindow.Handle;
            var helper = new WindowInteropHelper(_window) { Owner = acadHandle };

            _window.Closing += (s, e) =>
            {
                e.Cancel = true;
                _window.Hide();
            };

            _window.Show();
            LoggingService.Info("Enkesit Oku paneli açıldı");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_window != null)
            {
                _window.Closing -= null;
                _window.Close();
                _window = null;
            }
        }
    }
}
