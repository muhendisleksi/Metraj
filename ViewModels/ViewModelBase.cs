using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Metraj.ViewModels
{
    /// <summary>
    /// MVVM deseni için temel ViewModel sınıfı.
    /// INotifyPropertyChanged implementasyonu içerir.
    /// Thread-safe property updates ve proper disposal pattern sağlar.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int _disposed = 0;
        private readonly object _propertyLock = new object();

        /// <summary>
        /// PropertyChanged olayını tetikler
        /// </summary>
        /// <param name="propertyName">Değişen özellik adı (otomatik alınır)</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (IsDisposed) return;

            // Thread-safe event invocation
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Özellik değerini günceller ve gerekirse PropertyChanged olayını tetikler.
        /// Thread-safe implementasyon.
        /// </summary>
        /// <typeparam name="T">Özellik tipi</typeparam>
        /// <param name="field">Backing field referansı</param>
        /// <param name="value">Yeni değer</param>
        /// <param name="propertyName">Özellik adı (otomatik alınır)</param>
        /// <returns>Değer değiştiyse true, aksi halde false</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (IsDisposed) return false;

            lock (_propertyLock)
            {
                if (Equals(field, value)) return false;
                field = value;
            }

            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Birden fazla özelliğin değiştiğini bildirir
        /// </summary>
        protected void OnPropertiesChanged(params string[] propertyNames)
        {
            if (IsDisposed || propertyNames == null) return;

            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Finalizer - Dispose çağrılmadığında unmanaged kaynakları temizler
        /// </summary>
        ~ViewModelBase()
        {
            Dispose(false);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Thread-safe disposal check using Interlocked
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (disposing)
            {
                // Managed kaynakları temizle
                // Event handler'ları düzgün şekilde temizle
                PropertyChanged = null;
            }
        }

        /// <summary>
        /// Nesnenin dispose edilip edilmediğini kontrol eder
        /// </summary>
        protected bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) != 0;

        /// <summary>
        /// Dispose edilmiş nesne için exception fırlatır
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
