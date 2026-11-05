using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using AvaloniaEdit.AvaloniaMocks;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using NUnit.Framework;

namespace AvaloniaEdit.Tests.Editing
{
    [TestFixture]
    internal class TextAreaUndoTests
    {
        private const string Text = "line one\nline two\nline three";

        [AvaloniaTest]
        public void Editing_Should_Not_Keep_A_Removed_Editor_Alive()
        {
            UnitTestApplication.InitializeStyles();
            var document = new TextDocument(Text);
            var window = new Window { Width = 400, Height = 300 };
            window.Show();

            WeakReference editor = ShowEditAndRemoveEditor(window, document);

            // Headless releases removed controls on the next render.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.That(editor.IsAlive, Is.False, "The document's undo history keeps the editor alive.");
            Assert.That(document.UndoStack.CanUndo);
            window.Close();
        }

        [AvaloniaTest]
        public void Undo_Should_Restore_Caret_And_Selection()
        {
            var textArea = new TextArea { Document = new TextDocument(Text) };
            textArea.Selection = Selection.Create(textArea, 5, 8);
            textArea.Caret.Offset = 8;

            textArea.Document.Replace(0, 4, "LINE");
            textArea.ClearSelection();
            textArea.Caret.Offset = 0;
            textArea.Document.UndoStack.Undo();

            Assert.That(textArea.Document.Text, Is.EqualTo(Text));
            Assert.That(textArea.Selection, Is.InstanceOf<SimpleSelection>());
            Assert.That(textArea.Selection.GetText(), Is.EqualTo("one"));
            Assert.That(textArea.Caret.Offset, Is.EqualTo(8));
        }

        [AvaloniaTest]
        public void Undo_Should_Restore_Rectangle_Selection()
        {
            var textArea = new TextArea { Document = new TextDocument(Text) };
            var start = new TextViewPosition(1, 6);
            var end = new TextViewPosition(2, 9);
            textArea.Selection = new RectangleSelection(textArea, start, end);

            textArea.Document.Insert(0, "// ");
            textArea.ClearSelection();
            textArea.Document.UndoStack.Undo();

            Assert.That(textArea.Selection, Is.InstanceOf<RectangleSelection>());
            Assert.That(textArea.Selection.StartPosition, Is.EqualTo(start));
            Assert.That(textArea.Selection.EndPosition, Is.EqualTo(end));
            Assert.That(textArea.Selection.GetText(), Is.EqualTo("one" + Environment.NewLine + "two"));
        }

        [AvaloniaTest]
        public void Undo_Should_Restore_Rectangle_Selection_Exactly_With_Mixed_Font_Sizes()
        {
            var textArea = new TextArea { Document = new TextDocument("abcdefghij\nklmnopqrst") };
            textArea.TextView.LineTransformers.Add(new FontSizePerLine(12, 31));
            textArea.Selection = new RectangleSelection(textArea, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
            textArea.Selection.ReplaceSelectionWithText("x");
            Selection before = textArea.Selection;

            textArea.Document.Insert(textArea.Document.TextLength, "!");
            textArea.Document.UndoStack.Undo();

            Assert.That(textArea.Selection, Is.EqualTo(before));
            Assert.That(textArea.Selection.GetText(), Is.EqualTo(before.GetText()));
        }

        [AvaloniaTest]
        public void Undo_Should_Work_After_The_Text_Area_Lost_Its_Document()
        {
            var document = new TextDocument(Text);
            var textArea = new TextArea { Document = document };
            textArea.Selection = Selection.Create(textArea, 5, 8);
            document.Replace(0, 4, "LINE");
            textArea.Document = null;

            Assert.DoesNotThrow(() => document.UndoStack.Undo());
            Assert.That(document.Text, Is.EqualTo(Text));
        }

        private sealed class FontSizePerLine(params double[] sizes) : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                double size = sizes[line.LineNumber - 1];
                ChangeLinePart(line.Offset, line.EndOffset, e => e.TextRunProperties.SetFontRenderingEmSize(size));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ShowEditAndRemoveEditor(Window window, TextDocument document)
        {
            var editor = new TextEditor { Document = document };
            window.Content = editor;
            window.UpdateLayout();

            document.Insert(0, "// ");
            document.Replace(3, 4, "LINE");
            window.UpdateLayout();

            window.Content = null;
            editor.Document = null;
            return new WeakReference(editor);
        }
    }
}
