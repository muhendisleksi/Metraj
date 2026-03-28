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

        public CizgiTanimi Zemin => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.Zemin);
        public CizgiTanimi ProjeKotu => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.ProjeKotu);
        public CizgiTanimi SiyirmaTaban => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.SiyirmaTaban);
        public CizgiTanimi UstyapiAltKotu => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.UstyapiAltKotu);
    }
}
