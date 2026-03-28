using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Models.YolEnkesit
{
    public class AnchorNokta
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public ObjectId TextId { get; set; }
    }
}
