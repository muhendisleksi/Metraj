using System;
using Microsoft.Extensions.DependencyInjection;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Services;
using Metraj.Services.Interfaces;
using Metraj.ViewModels;

namespace Metraj.Infrastructure
{
    public static class ServiceContainer
    {
        private static IServiceProvider _provider;
        private static readonly object _lock = new object();
        private static bool _initialized;

        public static IServiceProvider Provider
        {
            get
            {
                if (_provider == null)
                    throw new InvalidOperationException("ServiceContainer has not been initialized. Call Initialize() first.");
                return _provider;
            }
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                var services = new ServiceCollection();
                ConfigureServices(services);
                _provider = services.BuildServiceProvider();
                _initialized = true;

                LoggingService.Info("ServiceContainer initialized successfully");
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // AutoCAD Abstraction Layer
            services.AddTransient<IDocumentContext, AutoCadDocumentContext>();
            services.AddTransient<IEditorService, AutoCadEditorService>();
            services.AddTransient<IEntityService, AutoCadEntityService>();

            // Calculation services - transient
            services.AddTransient<IUzunlukHesapService, UzunlukHesapService>();
            services.AddTransient<IAlanHesapService, AlanHesapService>();
            services.AddTransient<IHacimHesapService, HacimHesapService>();
            services.AddTransient<IToplamaService, ToplamaService>();
            services.AddTransient<IEnKesitAlanService, EnKesitAlanService>();

            // Annotation & Export services
            services.AddTransient<IAnnotationService, AnnotationService>();
            services.AddTransient<IExcelExportService, ExcelExportService>();
            services.AddSingleton<ICivil3dService, Civil3dService>();

            // Yol Metraj services
            services.AddTransient<IKatmanEslestirmeService, KatmanEslestirmeService>();
            services.AddTransient<IYolKesitService, YolKesitService>();
            services.AddTransient<IYolKubajService, YolKubajService>();

            // ViewModels - singleton (UI state persists)
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<UzunlukViewModel>();
            services.AddSingleton<AlanViewModel>();
            services.AddSingleton<HacimViewModel>();
            services.AddSingleton<ToplamaViewModel>();
            services.AddSingleton<AyarlarViewModel>();
            services.AddSingleton<EnKesitAlanViewModel>();
            services.AddSingleton<YolMetrajViewModel>();
        }

        public static T GetService<T>() where T : class
        {
            return Provider.GetService<T>();
        }

        public static T GetRequiredService<T>() where T : class
        {
            return Provider.GetRequiredService<T>();
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                if (!_initialized) return;

                if (_provider is IDisposable disposable)
                    disposable.Dispose();

                _provider = null;
                _initialized = false;

                LoggingService.Info("ServiceContainer disposed");
            }
        }
    }
}
