using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Metraj.Infrastructure.AutoCAD
{
    public class AutoCadDocumentContext : IDocumentContext
    {
        private Document _document;
        private bool _disposed;

        public AutoCadDocumentContext() { RefreshDocument(); }

        public bool HasActiveDocument { get { RefreshDocument(); return _document != null; } }
        public Database Database { get { EnsureDocument(); return _document.Database; } }
        public Editor Editor { get { EnsureDocument(); return _document.Editor; } }

        public Transaction BeginTransaction() { EnsureDocument(); return _document.Database.TransactionManager.StartTransaction(); }

        public void LockDocument(Action<Database> action) { EnsureDocument(); using (_document.LockDocument()) { action(_document.Database); } }
        public T LockDocument<T>(Func<Database, T> func) { EnsureDocument(); using (_document.LockDocument()) { return func(_document.Database); } }

        public void WriteMessage(string message) { if (HasActiveDocument) _document.Editor.WriteMessage(message); }
        public void WriteMessage(string format, params object[] args) { if (HasActiveDocument) _document.Editor.WriteMessage(format, args); }

        private void RefreshDocument() { try { _document = Application.DocumentManager.MdiActiveDocument; } catch { _document = null; } }
        private void EnsureDocument() { RefreshDocument(); if (_document == null) throw new InvalidOperationException("No active AutoCAD document available."); }

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing) { if (_disposed) return; if (disposing) _document = null; _disposed = true; }
        ~AutoCadDocumentContext() { Dispose(false); }
    }
}
