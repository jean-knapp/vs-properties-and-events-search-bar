using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace PropertiesSearch
{
    /// <summary>
    /// Waits for the Properties tool window to exist and be visible, then finds
    /// its PropertyGrid and mounts the search bar. The pane content materializes
    /// lazily, so attachment is retried on a short timer after each trigger.
    /// </summary>
    internal sealed class PropertiesWindowWatcher : IVsWindowFrameEvents, IDisposable
    {
        // The Properties window's persistence guid (StandardToolWindows.PropertyBrowser),
        // unchanged across VS versions.
        private static readonly Guid PropertiesWindowGuid = new Guid("eefa5220-e298-11d0-8f78-00a0c9110057");

        private readonly AsyncPackage _package;
        private IVsUIShell7 _shell7;
        private uint _frameEventsCookie;

        private System.Windows.Forms.Timer _retryTimer;
        private int _retriesLeft;

        private SearchBarInjector _injector;   // non-null while attached

        internal PropertiesWindowWatcher(AsyncPackage package) => _package = package;

        public void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _shell7 = _package.GetService<SVsUIShell, IVsUIShell7>();
            if (_shell7 != null)
                _frameEventsCookie = _shell7.AdviseWindowFrameEvents(this);
            DiagLog.Write($"Watcher started (frame events: {(_shell7 != null ? "advised" : "UNAVAILABLE")}).");

            // The window may already be open (it usually is).
            BeginAttachAttempts();
        }

        // ── IVsWindowFrameEvents ───────────────────────────────────────────

        public void OnFrameCreated(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (IsPropertiesFrame(frame)) BeginAttachAttempts();
        }

        public void OnFrameIsVisibleChanged(IVsWindowFrame frame, bool newIsVisible)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (newIsVisible && IsPropertiesFrame(frame)) BeginAttachAttempts();
        }

        public void OnFrameDestroyed(IVsWindowFrame frame) { }
        public void OnFrameIsOnScreenChanged(IVsWindowFrame frame, bool newIsOnScreen) { }
        public void OnActiveFrameChanged(IVsWindowFrame oldFrame, IVsWindowFrame newFrame) { }

        private static bool IsPropertiesFrame(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (frame == null) return false;
            return frame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_GuidPersistenceSlot, out var slot)
                       == VSConstants.S_OK
                   && slot == PropertiesWindowGuid;
        }

        // ── Attachment ─────────────────────────────────────────────────────

        private void BeginAttachAttempts()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_injector != null) return;   // already mounted

            _retriesLeft = 15;               // ~4.5 s worth of attempts
            if (_retryTimer == null)
            {
                _retryTimer = new System.Windows.Forms.Timer { Interval = 300 };
                _retryTimer.Tick += (s, e) => TryAttach();
            }
            _retryTimer.Start();
            TryAttach();
        }

        private void TryAttach()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_injector != null)
            {
                _retryTimer?.Stop();
                return;
            }

            var grid = PropertyGridLocator.FindPropertyBrowser();
            if (grid == null)
            {
                if (--_retriesLeft <= 0)
                {
                    _retryTimer?.Stop();
                    DiagLog.Write("Gave up: no Properties window PropertyGrid found after retries.");
                }
                return;
            }

            _retryTimer?.Stop();
            try
            {
                _injector = new SearchBarInjector(grid);
                _injector.GridGone += OnGridGone;
                DiagLog.Write($"Search bar attached to {grid.GetType().FullName}.");
                ActivityLog.LogInformation("PropertiesSearch",
                    $"Search bar attached to Properties window grid ({grid.GetType().FullName}).");
            }
            catch (Exception ex)
            {
                DiagLog.Write("Attach FAILED: " + ex);
            }
        }

        // The grid's handle was destroyed (rare — e.g. shell recreated the pane).
        // Drop the injector and re-arm so the next frame event re-attaches.
        private void OnGridGone(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _injector?.Dispose();
            _injector = null;
            BeginAttachAttempts();
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _retryTimer?.Stop();
            _retryTimer?.Dispose();
            _retryTimer = null;

            _injector?.Dispose();
            _injector = null;

            if (_shell7 != null && _frameEventsCookie != 0)
            {
                _shell7.UnadviseWindowFrameEvents(_frameEventsCookie);
                _frameEventsCookie = 0;
            }
        }
    }
}
