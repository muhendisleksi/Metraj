using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public interface IAnchorTaramaService
    {
        List<AnchorNokta> AnchorTara(IEnumerable<ObjectId> entityIds);
    }
}
