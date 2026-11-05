using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using NUnit.Framework;
using TextMateSharp.Grammars;

namespace AvaloniaEdit.Tests.TextMate
{
    [TestFixture]
    internal class LongLineLimitTextMateTests
    {
        [AvaloniaTest]
        [TestCase(3000, false)]
        [TestCase(10_000, true)]
        public void TextMate_Should_Highlight_Lines_Up_To_The_Limit(int limit, bool highlighted)
        {
            string longLine = string.Concat(Enumerable.Repeat("int x; ", 1000));
            var editor = new TextEditor { Document = new TextDocument(longLine + "\nint y;") };
            editor.Options.LongLineLimit = limit;
            TextView textView = editor.TextArea.TextView;
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            var textMate = editor.InstallTextMate(registryOptions);
            textMate.SetGrammar(registryOptions.GetScopeByLanguageId("csharp"));

            // TextMate tokenizes on a worker thread; the short second line shows when it's done.
            var stopwatch = Stopwatch.StartNew();
            while (CountColors(textView, 2) < 2)
            {
                Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(10), "TextMate never highlighted the editor.");
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.That(CountColors(textView, 1) > 1, Is.EqualTo(highlighted));
            textMate.Dispose();
        }

        private static int CountColors(TextView textView, int lineNumber)
        {
            textView.Measure(new Size(400, 300));
            textView.Arrange(new Rect(0, 0, 400, 300));
            VisualLine line = textView.GetOrConstructVisualLine(textView.Document.GetLineByNumber(lineNumber));
            return line.Elements
                .Select(e => (e.TextRunProperties.ForegroundBrush as ISolidColorBrush)?.Color)
                .Distinct()
                .Count();
        }
    }
}
