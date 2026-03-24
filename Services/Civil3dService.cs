using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class Civil3dService : ICivil3dService
    {
        public bool Mevcut => Civil3dDetector.IsCivil3dAvailable();

        public double FeatureLineUzunluk(ObjectId flId)
        {
            if (!Mevcut || flId.IsNull) return 0;

            try
            {
                // Civil 3D FeatureLine length via reflection to avoid compile-time dependency
                using (var tr = flId.Database.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(flId, OpenMode.ForRead);
                    var lengthProp = entity.GetType().GetProperty("Length");
                    if (lengthProp != null)
                    {
                        var result = (double)lengthProp.GetValue(entity);
                        tr.Commit();
                        return result;
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("FeatureLine uzunluk hatasi", ex);
            }

            return 0;
        }

        public double AlignmentUzunluk(ObjectId alignId)
        {
            if (!Mevcut || alignId.IsNull) return 0;

            try
            {
                using (var tr = alignId.Database.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(alignId, OpenMode.ForRead);
                    var lengthProp = entity.GetType().GetProperty("Length");
                    if (lengthProp != null)
                    {
                        var result = (double)lengthProp.GetValue(entity);
                        tr.Commit();
                        return result;
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Alignment uzunluk hatasi", ex);
            }

            return 0;
        }
    }
}
