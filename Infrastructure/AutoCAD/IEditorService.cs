using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Infrastructure.AutoCAD
{
    public interface IEditorService
    {
        PromptPointResult GetPoint(string message, bool allowNone = false);
        PromptPointResult GetPoint(string message, Point3d basePoint);
        PromptResult GetString(string message, string defaultValue = null, bool allowSpaces = true);
        PromptDoubleResult GetDouble(string message, double? defaultValue = null);
        PromptIntegerResult GetInteger(string message, int? defaultValue = null);
        PromptResult GetKeywords(string message, string[] keywords, string defaultKeyword = null);
        PromptSelectionResult GetSelection(string message = null);
        PromptEntityResult GetEntity(string message);
        PromptResult Drag(DrawJig jig);
        void WriteMessage(string message);
        void WriteMessage(string format, params object[] args);
        void SetWorldUCS();
    }
}
