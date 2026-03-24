using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Metraj.Infrastructure.AutoCAD
{
    public interface IDocumentContext : IDisposable
    {
        bool HasActiveDocument { get; }
        Database Database { get; }
        Editor Editor { get; }
        Transaction BeginTransaction();
        void LockDocument(Action<Database> action);
        T LockDocument<T>(Func<Database, T> func);
        void WriteMessage(string message);
        void WriteMessage(string format, params object[] args);
    }
}
