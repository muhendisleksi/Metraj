using System;
using System.Collections.Generic;
using System.Linq;

namespace Metraj.Models
{
    public class YolKesitVerisi
    {
        public double Istasyon { get; set; }                    // metre cinsinden (0, 20, 40, ...)
        public string IstasyonMetni { get; set; }               // "0+000", "0+020" formatında
        public string KolonHarfi { get; set; }                   // "A", "B", ..., "Z", "AA", ...
        public List<KatmanAlanBilgisi> KatmanAlanlari { get; set; } = new List<KatmanAlanBilgisi>();
        public double ToplamKaziAlani { get; set; }             // m²
        public double ToplamDolguAlani { get; set; }            // m²
        public DateTime Tarih { get; set; } = DateTime.Now;
        public string Aciklama { get; set; }

        public double MalzemeAlaniGetir(string malzemeAdi)
        {
            var katman = KatmanAlanlari.FirstOrDefault(k =>
                k.MalzemeAdi.Equals(malzemeAdi, StringComparison.OrdinalIgnoreCase));
            return katman?.Alan ?? 0;
        }
    }
}
