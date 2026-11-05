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
