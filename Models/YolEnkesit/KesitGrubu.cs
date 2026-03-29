using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Models.YolEnkesit
{
    public class KesitGrubu
    {
        /// <summary>Dikey cizgi esigi: Y/X orani bundan buyukse dikey kabul edilir</summary>
        private const double DikeyOranEsigi = 5.0;
        /// <summary>Nokta birlesme esigi</summary>
        private const double MergeTolerans = 0.01;

        public AnchorNokta Anchor { get; set; }
        public List<CizgiTanimi> Cizgiler { get; set; } = new List<CizgiTanimi>();
        public List<ObjectId> TextObjeler { get; set; } = new List<ObjectId>();
        public List<AlanHesapSonucu> HesaplananAlanlar { get; set; } = new List<AlanHesapSonucu>();
        public List<TabloKiyasSonucu> TabloKiyaslari { get; set; } = new List<TabloKiyasSonucu>();
        public DogrulamaDurumu Durum { get; set; } = DogrulamaDurumu.Bekliyor;

        /// <summary>CL eksen cizgisinin X koordinati (kesitin orta noktasi)</summary>
        public double? CL_X { get; set; }

        /// <summary>CL cizgisi bulunamadiysa true</summary>
        public bool CLEksik => !CL_X.HasValue;

        // Birlesik shortcut property'ler — ayni roldeki tum yatay parcalari birlestir
        public CizgiTanimi Zemin => BirlesikCizgiAl(CizgiRolu.Zemin);
        public CizgiTanimi ProjeCizgisi => BirlesikCizgiAl(CizgiRolu.ProjeCizgisi);
        public CizgiTanimi Siyirma => BirlesikCizgiAl(CizgiRolu.Siyirma);

        /// <summary>
        /// Ayni roldeki tum yatay cizgileri birlestirerek tek CizgiTanimi dondurur.
        /// Tek cizgi varsa dogrudan onu dondurur (merge yapmaz).
        /// Dikey cizgileri (X araligi &lt; 2 birim) filtreler.
        /// </summary>
        private CizgiTanimi BirlesikCizgiAl(CizgiRolu rol)
        {
            var parcalar = Cizgiler?.Where(c => c.Rol == rol).ToList();
            if (parcalar == null || parcalar.Count == 0) return null;
            if (parcalar.Count == 1) return parcalar[0];

            // Dikey cizgileri filtrele (Y/X orani > 5 ise dikey eleman)
            var yataylar = parcalar.Where(c => !CizgiDikeyMi(c)).ToList();

            if (yataylar.Count == 0)
                return parcalar.OrderByDescending(c => c.Noktalar.Max(p => p.X) - c.Noktalar.Min(p => p.X)).First();
            if (yataylar.Count == 1) return yataylar[0];

            // Birden fazla yatay: noktalarini birlestir
            var tumNoktalar = yataylar.SelectMany(c => c.Noktalar).OrderBy(p => p.X).ToList();
            var birlesik = new List<Point2d>(tumNoktalar.Count) { tumNoktalar[0] };
            for (int i = 1; i < tumNoktalar.Count; i++)
            {
                var son = birlesik[birlesik.Count - 1];
                var yeni = tumNoktalar[i];
                if (Math.Abs(yeni.X - son.X) < MergeTolerans)
                {
                    birlesik[birlesik.Count - 1] = new Point2d((son.X + yeni.X) / 2, (son.Y + yeni.Y) / 2);
                    continue;
                }
                birlesik.Add(yeni);
            }

            // Ilk parcanin metadata'sini kullan, noktalar birlesik
            var ilk = yataylar[0];
            return new CizgiTanimi
            {
                EntityId = ilk.EntityId,
                Rol = rol,
                LayerAdi = ilk.LayerAdi,
                RenkIndex = ilk.RenkIndex,
                Noktalar = birlesik,
                OtomatikAtanmis = ilk.OtomatikAtanmis,
                OrtalamaY = birlesik.Average(p => p.Y)
            };
        }

        /// <summary>
        /// Y/X orani > 5 ise dikey eleman (hendek kenari, CL, sev).
        /// 0.5m'den dar ve Y/X > 5 → dikey. Aksi halde yatay tabaka parcasi.
        /// </summary>
        private static bool CizgiDikeyMi(CizgiTanimi c)
        {
            if (c.Noktalar.Count < 2) return true;
            double xRange = c.Noktalar.Max(p => p.X) - c.Noktalar.Min(p => p.X);
            double yRange = c.Noktalar.Max(p => p.Y) - c.Noktalar.Min(p => p.Y);
            if (xRange < 0.01) return true; // neredeyse sifir X → kesinlikle dikey
            return yRange / xRange > DikeyOranEsigi;
        }
    }
}
