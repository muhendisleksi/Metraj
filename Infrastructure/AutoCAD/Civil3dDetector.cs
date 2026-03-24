using System;
using System.Reflection;

namespace Metraj.Infrastructure.AutoCAD
{
    public static class Civil3dDetector
    {
        private static bool? _isCivil3dAvailable;
        private static readonly object _lock = new object();

        public static bool IsCivil3dAvailable()
        {
            if (_isCivil3dAvailable.HasValue)
                return _isCivil3dAvailable.Value;

            lock (_lock)
            {
                if (_isCivil3dAvailable.HasValue)
                    return _isCivil3dAvailable.Value;

                try
                {
                    Assembly.Load("AeccDbMgd");
                    _isCivil3dAvailable = true;
                    Services.LoggingService.Info("Civil 3D detected - advanced features enabled");
                }
                catch
                {
                    _isCivil3dAvailable = false;
                    Services.LoggingService.Info("Civil 3D not detected - running in AutoCAD mode");
                }

                return _isCivil3dAvailable.Value;
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _isCivil3dAvailable = null;
            }
        }
    }
}
