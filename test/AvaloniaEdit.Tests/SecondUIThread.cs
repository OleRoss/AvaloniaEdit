using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Avalonia.Threading;
using NUnit.Framework;

namespace AvaloniaEdit.Tests
{
    /// <summary>
    /// A thread with its own Avalonia dispatcher, standing in for a second UI thread.
    /// </summary>
    /// <remarks>
    /// The headless platform can't run this dispatcher's main loop, so <see cref="Invoke{T}"/>
    /// runs the dispatcher's queued jobs after each call, as a main loop would.
    /// </remarks>
    internal sealed class SecondUIThread : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly BlockingCollection<Action> _work = new BlockingCollection<Action>();
        private readonly Thread _thread;
        private Dispatcher _dispatcher;

        public SecondUIThread()
        {
            using var started = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                started.Set();
                foreach (var action in _work.GetConsumingEnumerable())
                    action();
            });
            _thread.IsBackground = true;
            _thread.Start();
            started.Wait();
            Assert.That(_dispatcher, Is.Not.SameAs(Dispatcher.UIThread));
        }

        public void Invoke(Action action) => Invoke(() => { action(); return true; });

        /// <summary>
        /// Runs <paramref name="func"/> on this thread, then the jobs it queued on this
        /// thread's dispatcher, and rethrows any exception here.
        /// </summary>
        public T Invoke<T>(Func<T> func)
        {
            T result = default;
            ExceptionDispatchInfo error = null;
            using var done = new ManualResetEventSlim();
            _work.Add(() =>
            {
                try
                {
                    result = func();
                    _dispatcher.RunJobs();
                }
                catch (Exception ex)
                {
                    error = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    done.Set();
                }
            });
            if (!done.Wait(Timeout))
                Assert.Fail("The second UI thread did not finish in time.");
            error?.Throw();
            return result;
        }

        /// <summary>
        /// Runs <paramref name="condition"/> on this thread until it is true, for work that
        /// arrives from worker threads.
        /// </summary>
        public void WaitUntil(Func<bool> condition, string message)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!Invoke(condition))
            {
                if (stopwatch.Elapsed > Timeout)
                    Assert.Fail(message);
                Thread.Sleep(10);
            }
        }

        public void Dispose()
        {
            _work.CompleteAdding();
            _thread.Join(Timeout);
            _work.Dispose();
        }
    }
}
