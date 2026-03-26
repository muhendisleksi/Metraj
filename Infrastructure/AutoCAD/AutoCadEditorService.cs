using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Metraj.Infrastructure.AutoCAD
{
    /// <summary>
    /// AutoCAD implementation of IEditorService.
    /// Provides editor operations with null safety.
    /// </summary>
    public class AutoCadEditorService : IEditorService
    {
        private Editor GetEditor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("No active AutoCAD document available.");
            }
            return doc.Editor;
        }

        public PromptPointResult GetPoint(string message, bool allowNone = false)
        {
            var ed = GetEditor();
            var options = new PromptPointOptions(message)
            {
                AllowNone = allowNone
            };
            return ed.GetPoint(options);
        }

        public PromptPointResult GetPoint(string message, Point3d basePoint)
        {
            var ed = GetEditor();
            var options = new PromptPointOptions(message)
            {
                BasePoint = basePoint,
                UseBasePoint = true
            };
            return ed.GetPoint(options);
        }

        public PromptResult GetString(string message, string defaultValue = null, bool allowSpaces = true)
        {
            var ed = GetEditor();
            var options = new PromptStringOptions(message)
            {
                AllowSpaces = allowSpaces
            };

            if (!string.IsNullOrEmpty(defaultValue))
            {
                options.DefaultValue = defaultValue;
                options.UseDefaultValue = true;
            }

            return ed.GetString(options);
        }

        public PromptDoubleResult GetDouble(string message, double? defaultValue = null)
        {
            var ed = GetEditor();
            var options = new PromptDoubleOptions(message);

            if (defaultValue.HasValue)
            {
                options.DefaultValue = defaultValue.Value;
                options.UseDefaultValue = true;
            }

            return ed.GetDouble(options);
        }

        public PromptIntegerResult GetInteger(string message, int? defaultValue = null)
        {
            var ed = GetEditor();
            var options = new PromptIntegerOptions(message);

            if (defaultValue.HasValue)
            {
                options.DefaultValue = defaultValue.Value;
                options.UseDefaultValue = true;
            }

            return ed.GetInteger(options);
        }

        public PromptResult GetKeywords(string message, string[] keywords, string defaultKeyword = null)
        {
            var ed = GetEditor();
            var options = new PromptKeywordOptions(message);

            foreach (var keyword in keywords)
            {
                options.Keywords.Add(keyword);
            }

            if (!string.IsNullOrEmpty(defaultKeyword))
            {
                options.Keywords.Default = defaultKeyword;
            }

            return ed.GetKeywords(options);
        }

        public PromptSelectionResult GetSelection(string message = null)
        {
            var ed = GetEditor();

            if (string.IsNullOrEmpty(message))
            {
                return ed.GetSelection();
            }

            var options = new PromptSelectionOptions
            {
                MessageForAdding = message
            };
            return ed.GetSelection(options);
        }

        public PromptEntityResult GetEntity(string message)
        {
            var ed = GetEditor();
            var options = new PromptEntityOptions(message);
            return ed.GetEntity(options);
        }

        public PromptResult Drag(DrawJig jig)
        {
            var ed = GetEditor();
            return ed.Drag(jig);
        }

        public void WriteMessage(string message)
        {
            try
            {
                var ed = GetEditor();
                ed.WriteMessage(message);
            }
            catch
            {
                // Silently fail if no editor available
            }
        }

        public void WriteMessage(string format, params object[] args)
        {
            try
            {
                var ed = GetEditor();
                ed.WriteMessage(format, args);
            }
            catch
            {
                // Silently fail if no editor available
            }
        }

        public void SetWorldUCS()
        {
            var ed = GetEditor();
            ed.CurrentUserCoordinateSystem = Matrix3d.Identity;
        }

        public PromptPointResult GetPointWithKeywords(string message, string[] keywords)
        {
            var ed = GetEditor();
            var ppo = new PromptPointOptions(message);
            ppo.AllowNone = true;
            ppo.AppendKeywordsToMessage = true;

            foreach (var kw in keywords)
                ppo.Keywords.Add(kw);

            return ed.GetPoint(ppo);
        }
    }
}
