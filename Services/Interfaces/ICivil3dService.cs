using Autodesk.AutoCAD.DatabaseServices;

namespace Metraj.Services.Interfaces
{
    public interface ICivil3dService
    {
        bool Mevcut { get; }
        double FeatureLineUzunluk(ObjectId flId);
        double AlignmentUzunluk(ObjectId alignId);
    }
}
