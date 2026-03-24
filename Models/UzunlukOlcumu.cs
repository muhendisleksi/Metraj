using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Models
{
    public class UzunlukOlcumu : OlcumSonucu
    {
        public double Uzunluk { get; set; }
        public string NesneTipi { get; set; }
        public string KatmanAdi { get; set; }
        public short RenkIndeksi { get; set; }
        public bool Civil3dNesnesi { get; set; }

        public UzunlukOlcumu()
        {
            Birim = "m";
        }
    }
}
