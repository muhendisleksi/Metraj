using System.Collections.Generic;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public interface ICizgiRolAtamaService
    {
        ReferansKesitSablonu KalibrasyonOlustur(KesitGrubu referansKesit, List<CizgiTanimi> rolAtanmisCizgiler);
        void OtomatikRolAta(KesitGrubu kesit, ReferansKesitSablonu sablon);
        void TopluRolAta(List<KesitGrubu> kesitler, ReferansKesitSablonu sablon);
    }
}
