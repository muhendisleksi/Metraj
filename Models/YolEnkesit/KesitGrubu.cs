using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Models.YolEnkesit
{
    public class KesitGrubu
    {
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

        public CizgiTanimi Zemin => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.Zemin);
        public CizgiTanimi ProjeKotu => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.ProjeKotu);
        public CizgiTanimi SiyirmaTaban => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.SiyirmaTaban);
        public CizgiTanimi UstyapiAltKotu => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.UstyapiAltKotu);
        public CizgiTanimi EksenCizgisi => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.EksenCizgisi);
    }
}
