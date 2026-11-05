using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using NUnit.Framework;
using TextMateSharp.Grammars;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace AvaloniaEdit.Tests
{
    // An editor created on a thread with its own dispatcher must only use that dispatcher.
    // The headless platform cannot host a Window on a second thread (all headless windows
    // share the main thread's compositor), so these tests lay out and render the TextView
    // directly instead of showing a window.
    [TestFixture]
    internal class SecondUIThreadTests
    {
        private const string Code = "class C\n{\n    void M()\n    {\n    }\n}\n";

        [AvaloniaTest]
        public void TextEditor_Should_Work_On_A_Second_UI_Thread()
        {
            TextEditor editor = null;

            RunOnSecondUIThread(
                // Avalonia posts the first value of the binding that TextEditor sets up for
                // TextArea.Background to the main UI thread. That is Avalonia's binding
                // engine, not AvaloniaEdit, so it is drained before the editor is used.
                create: () => editor = new TextEditor { Document = new TextDocument(Code) },
                use: dispatcher =>
                {
                    TextArea textArea = editor.TextArea;
                    TextView textView = textArea.TextView;
                    textArea.ReadOnlySectionProvider = new TextSegmentReadOnlySectionProvider<TextSegment>(editor.Document);
                    Layout(textView, 300);
                    Assert.IsTrue(textView.VisualLinesValid);
                    Render(textView);

                    textArea.Caret.Offset = 0;
                    textArea.PerformTextInput("// ");
                    textArea.Caret.Show();
                    Assert.AreEqual("// class C", editor.Document.GetText(editor.Document.Lines[0]));

                    textArea.Selection = Selection.Create(textArea, 3, 8);
                    Assert.AreEqual("class", textArea.Selection.GetText());
                    dispatcher.RunJobs();

                    SearchPanel searchPanel = SearchPanel.Install(editor);
                    ApplicationCommands.Find.Execute(null, textArea);
                    Assert.AreEqual("class", searchPanel.SearchPattern);
                    searchPanel.Close();
                    dispatcher.RunJobs();

                    FoldingManager foldingManager = FoldingManager.Install(textArea);
                    foldingManager.CreateFolding(editor.Document.GetOffset(2, 1), editor.Document.TextLength - 1).IsFolded = true;
                    Layout(textView, 300);
                    Assert.AreEqual(2, textView.VisualLines.Count(l => l.FirstDocumentLine.LineNumber <= 2));
                    Render(textView);

                    var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                    var textMate = editor.InstallTextMate(registryOptions);
                    textMate.SetGrammar(registryOptions.GetScopeByLanguageId("csharp"));
                    foldingManager.Clear();

                    // TextMate tokenizes on a worker thread and posts the redraw to the
                    // editor's dispatcher. Pump this thread's dispatcher until it arrives.
                    var stopwatch = Stopwatch.StartNew();
                    while (CountForegrounds(textView, 3) < 2)
                    {
                        Assert.Less(stopwatch.Elapsed.TotalSeconds, 10, "TextMate highlighting never reached the editor.");
                        Thread.Sleep(10);
                        dispatcher.RunJobs();
                        Layout(textView, 300);
                    }
                    Render(textView);

                    // Scrolling makes TextMate tokenize the new viewport.
                    Layout(textView, 20);
                    textView.SetScrollOffset(new Vector(0, textView.DefaultLineHeight * 2));
                    dispatcher.RunJobs();
                    Render(textView);

                    textMate.Dispose();
                    FoldingManager.Uninstall(foldingManager);
                    dispatcher.RunJobs();
                });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Highlighted_TextView_Should_Work_On_A_Second_UI_Thread()
        {
            RunOnSecondUIThread(
                create: () => { },
                use: dispatcher =>
                {
                    var document = new TextDocument(Code);
                    var textView = new TextView { Document = document };
                    var highlighter = new DocumentHighlighter(document, HighlightingManager.Instance.GetDefinition("C#"));
                    textView.LineTransformers.Add(new HighlightingColorizer(highlighter));
                    Layout(textView, 300);
                    Assert.IsTrue(textView.VisualLinesValid);
                    Render(textView);
                    dispatcher.RunJobs();
                });

            AssertNothingQueuedOnMainUIThread();
        }

        private static void AssertNothingQueuedOnMainUIThread()
        {
            Assert.IsFalse(Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.SystemIdle),
                "The second-thread editor queued work on the main UI thread.");
        }

        private static void Layout(TextView textView, double height)
        {
            textView.Measure(new Size(400, height));
            textView.Arrange(new Rect(0, 0, 400, height));
        }

        private static void Render(Visual visual)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(400, 300));
            bitmap.Render(visual);
        }

        private static int CountForegrounds(TextView textView, int lineNumber)
        {
            VisualLine line = textView.GetVisualLine(lineNumber);
            if (line == null)
                return 0;
            return line.Elements
                .Select(e => (e.TextRunProperties.ForegroundBrush as ISolidColorBrush)?.Color)
                .Distinct()
                .Count();
        }

        // Runs create, then use, on a new thread with its own dispatcher. Between the two,
        // the main UI thread runs its queued jobs.
        private static void RunOnSecondUIThread(Action create, Action<Dispatcher> use)
        {
            Exception error = null;
            using var created = new ManualResetEventSlim();
            using var drained = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    Assert.AreNotSame(Dispatcher.UIThread, dispatcher);
                    try
                    {
                        create();
                    }
                    finally
                    {
                        created.Set();
                    }
                    drained.Wait();
                    use(dispatcher);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.Start();
            Assert.IsTrue(created.Wait(TimeSpan.FromSeconds(30)), "The second UI thread did not start.");
            Dispatcher.UIThread.RunJobs();
            drained.Set();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "The second UI thread did not finish.");
            if (error != null)
                ExceptionDispatchInfo.Throw(error);
        }
    }
}
