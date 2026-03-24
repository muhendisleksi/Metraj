using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Infrastructure.AutoCAD
{
    /// <summary>
    /// AutoCAD implementation of IEntityService.
    /// Provides entity CRUD operations with proper error handling.
    /// </summary>
    public class AutoCadEntityService : IEntityService
    {
        public ObjectId AddEntity(Transaction tr, Entity entity, string layerName = null)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var db = entity.Database ?? HostApplicationServices.WorkingDatabase;
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            if (!string.IsNullOrEmpty(layerName))
            {
                entity.Layer = layerName;
            }

            var id = modelSpace.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
            return id;
        }

        public T GetEntity<T>(Transaction tr, ObjectId id, OpenMode mode = OpenMode.ForRead) where T : DBObject
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (id.IsNull)
                throw new ArgumentException("ObjectId cannot be null", nameof(id));

            return tr.GetObject(id, mode) as T;
        }

        public void DeleteEntity(Transaction tr, ObjectId id)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (id.IsNull)
                return;

            var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
            entity?.Erase();
        }

        public void MoveEntity(Transaction tr, ObjectId id, Vector3d displacement)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (id.IsNull)
                return;

            var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
            entity?.TransformBy(Matrix3d.Displacement(displacement));
        }

        public void RotateEntity(Transaction tr, ObjectId id, Point3d basePoint, double angle)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (id.IsNull)
                return;

            var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
            entity?.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint));
        }

        public ObjectId EnsureLayer(Transaction tr, Database db, string layerName, short colorIndex)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(layerName))
                throw new ArgumentException("Layer name cannot be empty", nameof(layerName));

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (layerTable.Has(layerName))
            {
                var existingId = layerTable[layerName];
                var existingLayer = (LayerTableRecord)tr.GetObject(existingId, OpenMode.ForWrite);
                existingLayer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                existingLayer.IsOff = false;
                existingLayer.IsFrozen = false;
                existingLayer.IsLocked = false;
                return existingId;
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                IsOff = false,
                IsFrozen = false,
                IsLocked = false
            };

            var id = layerTable.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            return id;
        }

        public ObjectId EnsureTextStyle(Transaction tr, Database db, string styleName, string fontFile)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(styleName))
                throw new ArgumentException("Style name cannot be empty", nameof(styleName));

            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

            if (textStyleTable.Has(styleName))
            {
                return textStyleTable[styleName];
            }

            textStyleTable.UpgradeOpen();
            var style = new TextStyleTableRecord
            {
                Name = styleName,
                FileName = fontFile ?? "simplex.shx"
            };

            var id = textStyleTable.Add(style);
            tr.AddNewlyCreatedDBObject(style, true);
            return id;
        }

        public IEnumerable<ObjectId> GetEntitiesOnLayer(Transaction tr, Database db, string layerName)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(layerName))
                yield break;

            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in modelSpace)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity != null && string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return id;
                }
            }
        }

        public DBDictionary GetOrCreateExtensionDictionary(Transaction tr, Entity entity)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.ExtensionDictionary == ObjectId.Null)
            {
                entity.UpgradeOpen();
                entity.CreateExtensionDictionary();
            }

            return (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
        }
    }
}
