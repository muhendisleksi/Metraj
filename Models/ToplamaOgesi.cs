using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Models
{
    public class ToplamaOgesi
    {
        public string MetinDegeri { get; set; }
        public double SayisalDeger { get; set; }
        public ObjectId KaynakObjectId { get; set; }
        public Point3d Konum { get; set; }
        public string KatmanAdi { get; set; }
        public bool GecerliSayi { get; set; }
    }
}
