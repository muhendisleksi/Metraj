using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Entity kapali mi (Polyline.Closed, 3dFace, Hatch vb.).</summary>
        public bool KapaliMi { get; set; }

        /// <summary>Kapali entity'nin AutoCAD .Area degeri (m2). Kapali degilse 0.</summary>
        public double EntityAlani { get; set; }

        /// <summary>
        /// Cizginin dikey eleman, sev veya hendek baglantisi olup olmadigini kontrol eder.
        /// Tabaka cizgileri genis ve yatay, sev/hendek cizgileri dar ve diktir.
        /// True donerse bu cizgi tabaka hesabina ve onizlemeye dahil edilmemeli.
        /// </summary>
        public bool DikeyVeyaSevMi
        {
            get
            {
                if (Noktalar == null || Noktalar.Count < 2) return true;
                double xRange = Noktalar.Max(p => p.X) - Noktalar.Min(p => p.X);
                if (xRange < 0.01) return true;
                double yRange = Noktalar.Max(p => p.Y) - Noktalar.Min(p => p.Y);
                if (yRange / xRange > 5.0) return true;
                if (xRange < 2.0 && yRange > 2.0) return true;
                return false;
            }
        }
    }
}
