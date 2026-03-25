using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Models
{
    public class EnKesitAlanOlcumu
    {
        public string MalzemeAdi { get; set; }
        public double Alan { get; set; }
        public string KatmanAdi { get; set; }
        public string Yontem { get; set; }
        public List<ObjectId> KaynakNesneler { get; set; } = new List<ObjectId>();
    }
}
