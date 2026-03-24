using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Models
{
    public abstract class OlcumSonucu
    {
        public double Deger { get; set; }
        public string Birim { get; set; }
        public List<ObjectId> KaynakNesneler { get; set; } = new List<ObjectId>();
        public string Aciklama { get; set; }
    }
}
