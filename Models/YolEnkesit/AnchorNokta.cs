using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Models.YolEnkesit
{
    public class AnchorNokta
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public ObjectId TextId { get; set; }

        /// <summary>CL dikey cizgisinin X koordinati</summary>
        public double CL_X { get; set; }
        /// <summary>CL cizgisinin alt Y koordinati</summary>
        public double CL_MinY { get; set; }
        /// <summary>CL cizgisinin ust Y koordinati</summary>
        public double CL_MaxY { get; set; }
        /// <summary>CL cizgisinin ObjectId'si</summary>
        public ObjectId CL_EntityId { get; set; }
        /// <summary>CL eslesmesi dogrulanmis mi</summary>
        public bool CL_Dogrulandi { get; set; }
    }
}
