using System.Linq;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using AvaloniaEdit.Snippets;
using AvaloniaEdit.TextMate;
using NUnit.Framework;
using TextMateSharp.Grammars;
using Snippet = AvaloniaEdit.Snippets.Snippet;

namespace AvaloniaEdit.Tests
{
    /// <summary>
    /// An editor created on a second UI thread must use that thread's dispatcher, not
    /// <see cref="Dispatcher.UIThread"/>.
    /// </summary>
    /// <remarks>
    /// The headless platform can't show a Window on a second thread (headless windows share
    /// the main thread's compositor), so these tests lay out and render the TextView directly.
    /// Each test ends by checking that nothing was queued on the main UI thread.
    /// </remarks>
    [TestFixture]
    internal class SecondUIThreadTests
    {
        private const string Code = "class C\n{\n    void M()\n    {\n    }\n}\n";
        private static readonly Size ViewSize = new Size(400, 300);

        [AvaloniaTest]
        public void TextView_Should_Lay_Out_And_Render()
        {
            using var ui = new SecondUIThread();

            ui.Invoke(() =>
            {
                var textView = new TextView { Document = new TextDocument(Code) };
                Layout(textView);
                textView.EnsureVisualLines();
                Render(textView);

                Assert.That(textView.VisualLinesValid);
                Assert.That(textView.VisualLines, Has.Count.EqualTo(7));
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Typing_And_Selecting_Should_Work()
        {
            using var ui = new SecondUIThread();

            ui.Invoke(() =>
            {
                var textArea = new TextArea { Document = new TextDocument(Code) };
                textArea.ReadOnlySectionProvider = new TextSegmentReadOnlySectionProvider<TextSegment>(textArea.Document);
                Layout(textArea.TextView);

                textArea.Caret.Offset = 0;
                textArea.PerformTextInput("// ");
                textArea.Caret.Show();
                textArea.Selection = Selection.Create(textArea, 3, 8);
                Render(textArea.TextView);

                Assert.That(textArea.Document.GetText(textArea.Document.Lines[0]), Is.EqualTo("// class C"));
                Assert.That(textArea.Selection.GetText(), Is.EqualTo("class"));
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Key_Bindings_Should_Work_While_The_Main_Thread_Has_Focus()
        {
            // Creates the main thread's default key bindings.
            var mainTextArea = new TextArea();

            using var ui = new SecondUIThread();
            TextArea textArea = ui.Invoke(() =>
            {
                var t = new TextArea { Document = new TextDocument(Code) };
                t.Caret.Offset = 0;
                RaiseGotFocus(t);
                return t;
            });

            // Key commands run on the element that last got focus on the same thread.
            RaiseGotFocus(mainTextArea);
            Dispatcher.UIThread.RunJobs(); // the main-thread editor's own work

            ui.Invoke(() =>
            {
                PressKey(textArea, Key.Right);
                PressKey(textArea, Key.Back);

                Assert.That(textArea.Document.GetText(textArea.Document.Lines[0]), Is.EqualTo("lass C"));
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Find_Command_Should_Search_For_The_Selection()
        {
            using var ui = new SecondUIThread();
            TextEditor editor = CreateEditor(ui);

            ui.Invoke(() =>
            {
                SearchPanel searchPanel = SearchPanel.Install(editor);
                editor.Select(0, 5);

                ApplicationCommands.Find.Execute(null, editor.TextArea);

                Assert.That(searchPanel.SearchPattern, Is.EqualTo("class"));
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Folding_Should_Hide_Folded_Lines()
        {
            using var ui = new SecondUIThread();

            ui.Invoke(() =>
            {
                var textArea = new TextArea { Document = new TextDocument(Code) };
                FoldingManager foldingManager = FoldingManager.Install(textArea);
                foldingManager.CreateFolding(textArea.Document.GetOffset(2, 1), textArea.Document.TextLength - 1).IsFolded = true;
                Layout(textArea.TextView);
                Render(textArea.TextView);

                // Lines 2 to 6 are folded into one visual line.
                Assert.That(textArea.TextView.GetVisualLine(2).LastDocumentLine.LineNumber, Is.EqualTo(6));

                foldingManager.Clear();
                FoldingManager.Uninstall(foldingManager);
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void TextMate_Should_Highlight()
        {
            using var ui = new SecondUIThread();
            TextEditor editor = CreateEditor(ui);
            TextView textView = editor.TextArea.TextView;
            AvaloniaEdit.TextMate.TextMate.Installation textMate = null;

            ui.Invoke(() =>
            {
                var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                textMate = editor.InstallTextMate(registryOptions);
                textMate.SetGrammar(registryOptions.GetScopeByLanguageId("csharp"));
                Layout(textView);
            });

            // TextMate tokenizes on a worker thread, then posts a redraw to the editor's
            // dispatcher. "void" and "M" on line 3 get different colors once it arrives.
            ui.WaitUntil(() =>
            {
                Layout(textView);
                return CountColors(textView, 3) > 1;
            }, "TextMate highlighting never reached the editor.");

            ui.Invoke(() =>
            {
                Render(textView);

                // Scrolling makes TextMate tokenize the new viewport.
                Layout(textView, new Size(400, 20));
                textView.SetScrollOffset(new Vector(0, textView.DefaultLineHeight * 2));

                textMate.Dispose();
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void DocumentHighlighter_Should_Highlight()
        {
            using var ui = new SecondUIThread();

            ui.Invoke(() =>
            {
                var document = new TextDocument(Code);
                var textView = new TextView { Document = document };
                var highlighter = new DocumentHighlighter(document, HighlightingManager.Instance.GetDefinition("C#"));
                textView.LineTransformers.Add(new HighlightingColorizer(highlighter));
                Layout(textView);
                Render(textView);

                Assert.That(CountColors(textView, 1), Is.GreaterThan(1));
            });

            AssertNothingQueuedOnMainUIThread();
        }

        [AvaloniaTest]
        public void Shared_Defaults_Should_Work_After_The_Main_Thread_Used_Them()
        {
            // Static default brushes and pens are created by the first thread that uses them.
            RenderSharedDefaults(new TextEditor { Document = new TextDocument(Code) });

            using var ui = new SecondUIThread();
            TextEditor editor = CreateEditor(ui);
            ui.Invoke(() => RenderSharedDefaults(editor));

            AssertNothingQueuedOnMainUIThread();
        }

        // Renders shown spaces, a snippet field and search results.
        private static void RenderSharedDefaults(TextEditor editor)
        {
            TextArea textArea = editor.TextArea;
            textArea.Options.ShowSpaces = true;
            new Snippet { Elements = { new SnippetReplaceableTextElement { Text = "name" } } }.Insert(textArea);
            SearchPanel searchPanel = SearchPanel.Install(editor);
            searchPanel.SearchPattern = "class";
            searchPanel.Open();
            Layout(textArea.TextView);
            Render(textArea.TextView);
            searchPanel.Uninstall();
        }

        private static TextEditor CreateEditor(SecondUIThread ui)
        {
            TextEditor editor = ui.Invoke(() => new TextEditor { Document = new TextDocument(Code) });

            // TextEditor binds TextArea.Background to its own Background. Avalonia posts the
            // binding's first value to Dispatcher.UIThread, whichever thread made the binding.
            // Run that job now, so the final check only sees work queued later.
            Dispatcher.UIThread.RunJobs();

            return editor;
        }

        // Without a window, Focus() can't move focus, so raise the event focus would raise.
        private static void RaiseGotFocus(TextArea textArea)
        {
            textArea.RaiseEvent(new FocusChangedEventArgs(InputElement.GotFocusEvent) { NewFocusedElement = textArea });
        }

        private static void PressKey(TextArea textArea, Key key)
        {
            textArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        }

        private static void AssertNothingQueuedOnMainUIThread()
        {
            Assert.That(Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.SystemIdle), Is.False,
                "The second-thread editor queued work on the main UI thread.");
        }

        private static void Layout(TextView textView) => Layout(textView, ViewSize);

        private static void Layout(TextView textView, Size size)
        {
            textView.Measure(size);
            textView.Arrange(new Rect(size));
        }

        private static void Render(Visual visual)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)ViewSize.Width, (int)ViewSize.Height));
            bitmap.Render(visual);
        }

        private static int CountColors(TextView textView, int lineNumber)
        {
            VisualLine line = textView.GetVisualLine(lineNumber);
            if (line == null)
                return 0;
            return line.Elements
                .Select(e => (e.TextRunProperties.ForegroundBrush as ISolidColorBrush)?.Color)
                .Distinct()
                .Count();
        }
    }
}
