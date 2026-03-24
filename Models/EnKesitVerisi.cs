using System.Collections.Generic;

namespace Metraj.Models
{
    public class EnKesitVerisi
    {
        public double Istasyon { get; set; }
        public double ToplamAlan { get; set; }
        public List<Autodesk.AutoCAD.Geometry.Point2d> ProfilNoktalari { get; set; } = new List<Autodesk.AutoCAD.Geometry.Point2d>();
        public string Aciklama { get; set; }
    }
}
