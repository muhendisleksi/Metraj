using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Models.YolEnkesit
{
    /// <summary>
    /// Tek bir AutoCAD entity'sinin bellekte onbelleklenmis verisi.
    /// Transaction disinda kullanilabilir — tum AutoCAD API cagrilari
    /// cache olusturulurken yapilir, sonra saf bellek erisimi kalir.
    /// </summary>
    public class EntityCacheVerisi
    {
        public long Handle { get; set; }
        public ObjectId EntityId { get; set; }

        // Spatial filtreleme icin (GeometricExtents)
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        // Tip bilgisi
        public EntityKategori Kategori { get; set; }
        public string LayerAdi { get; set; }
        public short RenkIndex { get; set; }

        // Cizgi verileri (Polyline, Line, Face, Solid, Arc, Spline, Ellipse, Hatch)
        public List<Point2d> Noktalar { get; set; }
        public bool KapaliMi { get; set; }
        public double EntityAlani { get; set; }

        // Text verileri (DBText, MText, BlockReference, Table)
        public List<(string metin, double x, double y)> Textler { get; set; }
    }

    public enum EntityKategori
    {
        Cizgi,       // Polyline, Line, Face, Solid, Spline, Arc, Ellipse, Hatch
        Text,        // DBText, MText
        Blok,        // BlockReference — icindeki text'ler Textler listesinde
        Tablo,       // Table — hucre text'leri Textler listesinde
        Diger        // Desteklenmeyen tip
    }
}
