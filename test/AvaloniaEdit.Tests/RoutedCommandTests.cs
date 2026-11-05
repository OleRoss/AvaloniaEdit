using System;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using AvaloniaEdit.AvaloniaMocks;
using AvaloniaEdit.Document;
using NUnit.Framework;

namespace AvaloniaEdit.Tests
{
    [TestFixture]
    internal class RoutedCommandTests
    {
        [AvaloniaTest]
        public void Command_Should_Reach_The_Focused_Editor()
        {
            UnitTestApplication.InitializeStyles();
            var editor = new TextEditor { Document = new TextDocument("hello world") };
            var window = new Window { Width = 400, Height = 300, Content = editor };
            window.Show();
            window.UpdateLayout();
            editor.TextArea.Focus();

            ICommand selectAll = ApplicationCommands.SelectAll;
            Assert.That(selectAll.CanExecute(null));
            selectAll.Execute(null);

            Assert.That(editor.SelectedText, Is.EqualTo("hello world"));
            window.Close();
        }

        [AvaloniaTest]
        public void Focused_Editor_Should_Be_Collected_After_Removal()
        {
            UnitTestApplication.InitializeStyles();
            var window = new Window { Width = 400, Height = 300 };
            window.Show();

            WeakReference editor = ShowFocusAndRemoveEditor(window);

            // Headless releases removed controls on the next render.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.That(editor.IsAlive, Is.False, "The last focused element keeps the editor alive.");
            window.Close();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ShowFocusAndRemoveEditor(Window window)
        {
            var editor = new TextEditor { Document = new TextDocument("hello world") };
            window.Content = editor;
            window.UpdateLayout();
            editor.TextArea.Focus();
            Assert.That(editor.TextArea.IsFocused);

            window.Content = null;
            editor.Document = null;
            return new WeakReference(editor);
        }
    }
}
