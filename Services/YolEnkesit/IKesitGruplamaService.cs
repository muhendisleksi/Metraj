using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public interface IKesitGruplamaService
    {
        List<KesitGrubu> KesitGrupla(List<AnchorNokta> anchorlar, KesitPenceresi pencere, IEnumerable<ObjectId> entityIds);
    }
}
