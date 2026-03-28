using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Models.YolEnkesit
{
    public class CizgiTanimi
    {
        public ObjectId EntityId { get; set; }
        public CizgiRolu Rol { get; set; } = CizgiRolu.Tanimsiz;
        public string LayerAdi { get; set; }
        public short RenkIndex { get; set; }
        public List<Point2d> Noktalar { get; set; } = new List<Point2d>();
        public bool OtomatikAtanmis { get; set; }
        public double OrtalamaY { get; set; }
    }
}
