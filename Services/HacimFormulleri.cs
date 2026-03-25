using System;
using Metraj.Models;

namespace Metraj.Services
{
    internal static class HacimFormulleri
    {
        public static double Hesapla(double alan1, double alan2, double mesafe, HacimMetodu metot)
        {
            if (mesafe <= 0) return 0;

            switch (metot)
            {
                case HacimMetodu.Prismoidal:
                    double am = (alan1 + alan2) / 2.0;
                    return mesafe / 6.0 * (alan1 + 4.0 * am + alan2);

                case HacimMetodu.OrtalamaAlan:
                default:
                    return (alan1 + alan2) / 2.0 * mesafe;
            }
        }
    }
}
