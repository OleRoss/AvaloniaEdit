using System.Linq;
using Avalonia;
using Avalonia.Headless.NUnit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using NUnit.Framework;

namespace AvaloniaEdit.Tests.Rendering
{
    [TestFixture]
    internal class LongLineLimitTests
    {
        private const int LineLength = 5000;

        [AvaloniaTest]
        public void Default_Limit_Should_Collapse_The_Line_And_Refuse_Edits_In_The_Tail()
        {
            TextArea textArea = CreateTextArea();

            Assert.That(textArea.Options.LongLineLimit, Is.EqualTo(3000));
            Assert.That(IsCollapsed(textArea), Is.True);
            Assert.That(TypeAtColumn(textArea, 4000), Is.False);
        }

        [AvaloniaTest]
        [TestCase(10_000)]
        [TestCase(int.MaxValue)]
        public void Raised_Limit_Should_Show_The_Whole_Line_And_Accept_Edits(int limit)
        {
            TextArea textArea = CreateTextArea();
            textArea.Options.LongLineLimit = limit;
            Layout(textArea);

            Assert.That(IsCollapsed(textArea), Is.False);
            Assert.That(TypeAtColumn(textArea, 4000), Is.True);
        }

        [AvaloniaTest]
        public void Changing_The_Limit_Should_Rebuild_The_Visual_Lines()
        {
            TextArea textArea = CreateTextArea();
            Assert.That(IsCollapsed(textArea), Is.True);

            textArea.Options.LongLineLimit = 10_000;
            Layout(textArea);
            Assert.That(IsCollapsed(textArea), Is.False);

            textArea.Options.LongLineLimit = 3000;
            Layout(textArea);
            Assert.That(IsCollapsed(textArea), Is.True);
        }

        [AvaloniaTest]
        public void Edits_Should_Follow_The_Limit_The_Text_View_Shows()
        {
            TextArea textArea = CreateTextArea();
            textArea.Options.LongLineLimit = 10_000;
            textArea.TextView.Options = new TextEditorOptions();
            Layout(textArea);

            Assert.That(IsCollapsed(textArea), Is.True);
            Assert.That(TypeAtColumn(textArea, 4000), Is.False);
        }

        [AvaloniaTest]
        public void Line_At_The_Limit_Should_Accept_Edits_Everywhere()
        {
            const int limit = 8;
            TextArea textArea = CreateTextArea(new string('a', limit), limit);

            Assert.That(IsCollapsed(textArea), Is.False);
            for (int column = 1; column <= limit + 1; column++)
                Assert.That(Accepts(textArea, column), Is.True, $"insert at column {column}");
            for (int offset = 0; offset <= limit; offset++)
            {
                Assert.That(IsDeletable(textArea, offset, 0), Is.True, $"empty deletion at offset {offset}");
                Assert.That(IsDeletable(textArea, offset, 1), Is.True, $"deletion at offset {offset}");
            }
        }

        [AvaloniaTest]
        public void Line_Over_The_Limit_Should_Refuse_Edits_Only_In_Its_Collapsed_Tail()
        {
            const int limit = 8;
            TextArea textArea = CreateTextArea(new string('a', limit + 1), limit);

            // The view and the edit protection must agree on where the hidden tail starts.
            Assert.That(VisibleLength(textArea), Is.EqualTo(limit));
            for (int column = 1; column <= limit + 2; column++)
                Assert.That(Accepts(textArea, column), Is.EqualTo(column <= limit), $"insert at column {column}");
            for (int offset = 0; offset <= limit + 1; offset++)
            {
                Assert.That(IsDeletable(textArea, offset, 0), Is.EqualTo(offset < limit), $"empty deletion at offset {offset}");
                Assert.That(IsDeletable(textArea, offset, 1), Is.EqualTo(offset < limit), $"deletion at offset {offset}");
            }
        }

        private static TextArea CreateTextArea(string firstLine, int limit)
        {
            var textArea = new TextArea { Document = new TextDocument(firstLine + "\nb") };
            textArea.Options.LongLineLimit = limit;
            Layout(textArea);
            return textArea;
        }

        // Types a character at the column of the first line, reports whether it was inserted, and undoes it.
        private static bool Accepts(TextArea textArea, int column)
        {
            int length = textArea.Document.TextLength;
            textArea.Caret.Position = new TextViewPosition(1, column);
            textArea.PerformTextInput("x");
            if (textArea.Document.TextLength == length)
                return false;
            textArea.Document.UndoStack.Undo();
            return true;
        }

        // Whether the whole range at the offset may be deleted.
        private static bool IsDeletable(TextArea textArea, int offset, int length)
        {
            ISegment[] deletable = textArea.GetDeletableSegments(new SimpleSegment(offset, length));
            return deletable.Length == 1 && deletable[0].Offset == offset && deletable[0].Length == length;
        }

        // The number of characters of the first line that the view shows before "...".
        private static int VisibleLength(TextArea textArea)
        {
            VisualLine line = textArea.TextView.GetVisualLine(1);
            DocumentLine documentLine = line.FirstDocumentLine;
            return line.Elements.Last() is FormattedTextElement marker
                ? documentLine.Length - marker.DocumentLength
                : documentLine.Length;
        }

        private static TextArea CreateTextArea()
        {
            var textArea = new TextArea { Document = new TextDocument(new string('a', LineLength)) };
            Layout(textArea);
            return textArea;
        }

        private static void Layout(TextArea textArea)
        {
            textArea.TextView.Measure(new Size(400, 300));
            textArea.TextView.Arrange(new Rect(0, 0, 400, 300));
        }

        // A collapsed line ends in the "..." marker instead of its text.
        private static bool IsCollapsed(TextArea textArea)
        {
            VisualLine line = textArea.TextView.GetVisualLine(1);
            return line.Elements.Last() is FormattedTextElement;
        }

        // Types a character at the column and returns whether it was inserted.
        private static bool TypeAtColumn(TextArea textArea, int column)
        {
            textArea.Caret.Position = new TextViewPosition(1, column);
            textArea.PerformTextInput("x");
            return textArea.Document.TextLength == LineLength + 1;
        }
    }
}
