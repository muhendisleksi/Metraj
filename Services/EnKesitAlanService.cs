using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class EnKesitAlanService : IEnKesitAlanService
    {
        private readonly IDocumentContext _documentContext;

        public EnKesitAlanService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public double IkiCizgiArasiAlan(ObjectId ustCizgiId, ObjectId altCizgiId)
        {
            var ustNoktalar = PolylineNoktalariniAl(ustCizgiId);
            var altNoktalar = PolylineNoktalariniAl(altCizgiId);

            if (ustNoktalar.Count < 2 || altNoktalar.Count < 2)
                return 0;

            // Sort by X
            ustNoktalar = ustNoktalar.OrderBy(p => p.X).ToList();
            altNoktalar = altNoktalar.OrderBy(p => p.X).ToList();

            // Find overlapping X range
            double minX = Math.Max(ustNoktalar.First().X, altNoktalar.First().X);
            double maxX = Math.Min(ustNoktalar.Last().X, altNoktalar.Last().X);

            if (maxX <= minX) return 0;

            // Clip both lines to overlapping X range and add boundary points via interpolation
            var ustClipped = ClipToXRange(ustNoktalar, minX, maxX);
            var altClipped = ClipToXRange(altNoktalar, minX, maxX);

            // Build closed polygon: upper left-to-right, then lower right-to-left
            var polygon = new List<Point2d>();
            polygon.AddRange(ustClipped.OrderBy(p => p.X));
            polygon.AddRange(altClipped.OrderByDescending(p => p.X));

            return ShoelaceAlan(polygon);
        }

        public double KapaliNesneAlan(ObjectId nesneId)
        {
            if (nesneId.IsNull) return 0;

            using (var tr = _documentContext.BeginTransaction())
            {
                var entity = tr.GetObject(nesneId, OpenMode.ForRead) as Entity;
                if (entity == null) { tr.Commit(); return 0; }

                double alan = 0;

                switch (entity)
                {
                    case Polyline pl when pl.Closed:
                        alan = pl.Area;
                        break;

                    case Polyline2d pl2d when pl2d.Closed:
                        alan = pl2d.Area;
                        break;

                    case Polyline3d pl3d:
                        // 3D polyline - extract vertices and use Shoelace
                        var noktalar = new List<Point2d>();
                        foreach (ObjectId vId in pl3d)
                        {
                            var vertex = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                            if (vertex != null)
                                noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                        }
                        alan = ShoelaceAlan(noktalar);
                        break;

                    case Circle circle:
                        alan = Math.PI * circle.Radius * circle.Radius;
                        break;

                    case Hatch hatch:
                        alan = hatch.Area;
                        break;

                    case Face face:
                        var faceNoktalar = new List<Point2d>
                        {
                            new Point2d(face.GetVertexAt(0).X, face.GetVertexAt(0).Y),
                            new Point2d(face.GetVertexAt(1).X, face.GetVertexAt(1).Y),
                            new Point2d(face.GetVertexAt(2).X, face.GetVertexAt(2).Y),
                            new Point2d(face.GetVertexAt(3).X, face.GetVertexAt(3).Y)
                        };
                        alan = ShoelaceAlan(faceNoktalar);
                        break;

                    default:
                        break;
                }

                tr.Commit();
                return alan;
            }
        }

        public List<Point2d> PolylineNoktalariniAl(ObjectId entityId)
        {
            var noktalar = new List<Point2d>();
            if (entityId.IsNull) return noktalar;

            using (var tr = _documentContext.BeginTransaction())
            {
                var entity = tr.GetObject(entityId, OpenMode.ForRead) as Entity;

                switch (entity)
                {
                    case Polyline pl:
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                            noktalar.Add(pl.GetPoint2dAt(i));
                        break;

                    case Polyline2d pl2d:
                        foreach (ObjectId vId in pl2d)
                        {
                            var vertex = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                            if (vertex != null)
                                noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                        }
                        break;

                    case Polyline3d pl3d:
                        foreach (ObjectId vId in pl3d)
                        {
                            var vertex = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                            if (vertex != null)
                                noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                        }
                        break;

                    case Line line:
                        noktalar.Add(new Point2d(line.StartPoint.X, line.StartPoint.Y));
                        noktalar.Add(new Point2d(line.EndPoint.X, line.EndPoint.Y));
                        break;

                    case Face face:
                        noktalar.Add(new Point2d(face.GetVertexAt(0).X, face.GetVertexAt(0).Y));
                        noktalar.Add(new Point2d(face.GetVertexAt(1).X, face.GetVertexAt(1).Y));
                        noktalar.Add(new Point2d(face.GetVertexAt(2).X, face.GetVertexAt(2).Y));
                        noktalar.Add(new Point2d(face.GetVertexAt(3).X, face.GetVertexAt(3).Y));
                        break;

                    case Solid solid:
                        // 2D Solid (trace) — 3 veya 4 vertex
                        for (short vi = 0; vi < 4; vi++)
                            noktalar.Add(new Point2d(solid.GetPointAt(vi).X, solid.GetPointAt(vi).Y));
                        // 3. ve 4. vertex ayni olabilir (ucgen), tekrarlari cikar
                        if (noktalar.Count == 4
                            && Math.Abs(noktalar[2].X - noktalar[3].X) < 1e-6
                            && Math.Abs(noktalar[2].Y - noktalar[3].Y) < 1e-6)
                            noktalar.RemoveAt(3);
                        break;

                    case Spline spline:
                        // Kontrol noktalari yerine nurblar uzerinde esit aralikli ornekle
                        int ornekSayisi = Math.Max(spline.NumControlPoints * 3, 20);
                        double startParam = spline.StartParam;
                        double endParam = spline.EndParam;
                        double step = (endParam - startParam) / ornekSayisi;
                        for (int si = 0; si <= ornekSayisi; si++)
                        {
                            var pt = spline.GetPointAtParameter(startParam + si * step);
                            noktalar.Add(new Point2d(pt.X, pt.Y));
                        }
                        break;

                    case Arc arc:
                        // Yay: baslangic, orta, bitis + ara noktalar
                        int arcSamples = Math.Max((int)(arc.TotalAngle / (Math.PI / 18)), 4);
                        double arcStep = (arc.EndParam - arc.StartParam) / arcSamples;
                        for (int ai = 0; ai <= arcSamples; ai++)
                        {
                            var pt = arc.GetPointAtParameter(arc.StartParam + ai * arcStep);
                            noktalar.Add(new Point2d(pt.X, pt.Y));
                        }
                        break;

                    case Ellipse ellipse:
                        int ellSamples = 36;
                        double ellStep = (ellipse.EndParam - ellipse.StartParam) / ellSamples;
                        for (int ei = 0; ei <= ellSamples; ei++)
                        {
                            var pt = ellipse.GetPointAtParameter(ellipse.StartParam + ei * ellStep);
                            noktalar.Add(new Point2d(pt.X, pt.Y));
                        }
                        break;
                }

                tr.Commit();
            }

            return noktalar;
        }

        public EnKesitAlanOlcumu BoundaryAlanHesapla(Point3d nokta)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            // TraceBoundary returns curves at the picked point
            var boundaryResult = ed.TraceBoundary(nokta, true);

            if (boundaryResult == null || boundaryResult.Count == 0)
                return null;

            double alan = 0;
            string layerAdi = "";

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // Try to create a region from the boundary curves
                var curves = new DBObjectCollection();
                foreach (DBObject obj in boundaryResult)
                {
                    if (obj is Curve curve)
                        curves.Add(curve);
                }

                if (curves.Count > 0)
                {
                    // Try Region approach
                    try
                    {
                        var regions = Region.CreateFromCurves(curves);
                        if (regions.Count > 0)
                        {
                            var region = regions[0] as Region;
                            alan = region.Area;
                            region.Dispose();

                            for (int i = 1; i < regions.Count; i++)
                                ((DBObject)regions[i]).Dispose();
                        }
                    }
                    catch
                    {
                        // Fallback: if Region fails, calculate from polyline vertices
                        foreach (DBObject obj in boundaryResult)
                        {
                            if (obj is Polyline pl)
                            {
                                alan = pl.Area;
                                break;
                            }
                        }
                    }
                }

                // Try to find layer of the entity at the click point
                // Use a crossing selection at the point to find entities nearby
                try
                {
                    var selResult = ed.SelectCrossingWindow(
                        new Point3d(nokta.X - 0.5, nokta.Y - 0.5, 0),
                        new Point3d(nokta.X + 0.5, nokta.Y + 0.5, 0));
                    if (selResult.Status == PromptStatus.OK && selResult.Value.Count > 0)
                    {
                        var firstId = selResult.Value[0].ObjectId;
                        var entity = tr.GetObject(firstId, OpenMode.ForRead) as Entity;
                        layerAdi = entity?.Layer ?? "";
                    }
                }
                catch { }

                tr.Commit();
            }

            // Dispose boundary objects
            foreach (DBObject obj in boundaryResult)
                obj.Dispose();

            if (alan <= 0.0001) return null;

            return new EnKesitAlanOlcumu
            {
                MalzemeAdi = MalzemeAdiCikar(layerAdi),
                Alan = alan,
                KatmanAdi = layerAdi,
                Yontem = "Tıklama"
            };
        }

        public string MalzemeAdiCikar(string layerAdi)
        {
            if (string.IsNullOrWhiteSpace(layerAdi)) return "Bilinmeyen";

            string upper = layerAdi.ToUpperInvariant()
                .Replace("\u0130", "I").Replace("\u015E", "S").Replace("\u00C7", "C")
                .Replace("\u011E", "G").Replace("\u00DC", "U").Replace("\u00D6", "O");

            if (upper.Contains("ASINMA")) return "A\u015F\u0131nma";
            if (upper.Contains("BINDER")) return "Binder";
            if (upper.Contains("BITUM") || upper.Contains("BIT\u00DCM")) return "Bit\u00FCmen";
            if (upper.Contains("PLENTMISK") || upper.Contains("PMT")) return "Plentmisk";
            if (upper.Contains("ALTTEMEL") || upper.Contains("ALT_TEMEL")) return "AltTemel";
            if (upper.Contains("SIYIRMA")) return "S\u0131y\u0131rma";
            if (upper.Contains("YARMA") || upper.Contains("KAZI")) return "Yarma";
            if (upper.Contains("DOLGU")) return "Dolgu";
            if (upper.Contains("BTKONAN") || upper.Contains("BT_KONAN") || upper.Contains("YERINE_KONAN")) return "B.T. Yerine Konan";
            if (upper.Contains("BTKONMAYAN") || upper.Contains("BT_KONMAYAN") || upper.Contains("YERINE_KONMAYAN")) return "B.T. Yerine Konmayan";
            if (upper.Contains("STABILIZE") || upper.Contains("STAB")) return "Stabilize";
            if (upper.Contains("ZEMIN")) return "Zemin";
            if (upper.Contains("YOL")) return "Yol";
            if (upper.Contains("FORMASYON")) return "Formasyon";
            if (upper.Contains("BITKISEL") || upper.Contains("TOPRAK")) return "Bitkisel Toprak";
            if (upper.Contains("BANKET")) return "Banket";
            if (upper.Contains("HENDEK")) return "Hendek";

            return layerAdi;
        }

        /// <summary>
        /// Clips a polyline to an X range, interpolating Y values at boundaries
        /// </summary>
        public List<Point2d> ClipToXRange(List<Point2d> points, double minX, double maxX)
        {
            var result = new List<Point2d>();

            // Add interpolated point at minX if needed
            double yAtMinX = InterpolateY(points, minX);
            result.Add(new Point2d(minX, yAtMinX));

            // Add all points within range
            foreach (var p in points)
            {
                if (p.X >= minX && p.X <= maxX)
                    result.Add(p);
            }

            // Add interpolated point at maxX if needed
            double yAtMaxX = InterpolateY(points, maxX);
            result.Add(new Point2d(maxX, yAtMaxX));

            // Remove duplicates (same X within tolerance)
            return result.Distinct(new Point2dXComparer()).OrderBy(p => p.X).ToList();
        }

        /// <summary>
        /// Interpolates Y value at a given X along a polyline
        /// </summary>
        public double InterpolateY(List<Point2d> points, double x)
        {
            if (points.Count == 0) return 0;
            if (points.Count == 1) return points[0].Y;

            // Find the segment containing x
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                if ((x >= p1.X && x <= p2.X) || (x >= p2.X && x <= p1.X))
                {
                    if (Math.Abs(p2.X - p1.X) < 1e-10)
                        return (p1.Y + p2.Y) / 2.0;

                    double t = (x - p1.X) / (p2.X - p1.X);
                    return p1.Y + t * (p2.Y - p1.Y);
                }
            }

            // Extrapolate from nearest end
            if (x <= points.First().X) return points.First().Y;
            return points.Last().Y;
        }

        /// <summary>
        /// Shoelace formula for polygon area
        /// </summary>
        public double ShoelaceAlan(List<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0;

            double alan = 0;
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                alan += polygon[i].X * polygon[j].Y;
                alan -= polygon[j].X * polygon[i].Y;
            }

            return Math.Abs(alan) / 2.0;
        }

        /// <summary>
        /// Comparer for removing duplicate X points
        /// </summary>
        private class Point2dXComparer : IEqualityComparer<Point2d>
        {
            public bool Equals(Point2d a, Point2d b) => Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;
            public int GetHashCode(Point2d p) => 0; // Force Equals check for all
        }
    }
}
