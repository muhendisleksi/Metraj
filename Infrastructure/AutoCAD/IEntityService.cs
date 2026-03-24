using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Infrastructure.AutoCAD
{
    /// <summary>
    /// Entity CRUD operations abstraction for testability.
    /// </summary>
    public interface IEntityService
    {
        /// <summary>
        /// Creates a new entity and adds it to the database
        /// </summary>
        ObjectId AddEntity(Transaction tr, Entity entity, string layerName = null);

        /// <summary>
        /// Gets an entity by its ObjectId
        /// </summary>
        T GetEntity<T>(Transaction tr, ObjectId id, OpenMode mode = OpenMode.ForRead) where T : DBObject;

        /// <summary>
        /// Deletes an entity by its ObjectId
        /// </summary>
        void DeleteEntity(Transaction tr, ObjectId id);

        /// <summary>
        /// Moves an entity by a displacement vector
        /// </summary>
        void MoveEntity(Transaction tr, ObjectId id, Vector3d displacement);

        /// <summary>
        /// Rotates an entity around a point
        /// </summary>
        void RotateEntity(Transaction tr, ObjectId id, Point3d basePoint, double angle);

        /// <summary>
        /// Ensures a layer exists and returns its ObjectId
        /// </summary>
        ObjectId EnsureLayer(Transaction tr, Database db, string layerName, short colorIndex);

        /// <summary>
        /// Ensures a text style exists and returns its ObjectId
        /// </summary>
        ObjectId EnsureTextStyle(Transaction tr, Database db, string styleName, string fontFile);

        /// <summary>
        /// Gets all entities on a specific layer
        /// </summary>
        IEnumerable<ObjectId> GetEntitiesOnLayer(Transaction tr, Database db, string layerName);

        /// <summary>
        /// Gets the extension dictionary of an entity, creating it if necessary
        /// </summary>
        DBDictionary GetOrCreateExtensionDictionary(Transaction tr, Entity entity);
    }
}
