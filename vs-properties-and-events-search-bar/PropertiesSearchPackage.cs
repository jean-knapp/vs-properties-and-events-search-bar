using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace PropertiesSearch
{
    /// <summary>
    /// Loads in the background once the shell is up, then watches for the
    /// Properties tool window and mounts the search bar onto its grid.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class PropertiesSearchPackage : AsyncPackage
    {
        public const string PackageGuidString = "7f4c1a2e-9b3d-4e6f-8a21-c5d0e7b94a63";

        private PropertiesWindowWatcher _watcher;

        protected override async Task InitializeAsync(CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // Everything we do (window enumeration, WinForms controls) is UI-thread work.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DiagLog.Write("Package loaded.");
            _watcher = new PropertiesWindowWatcher(this);
            _watcher.Start();
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
            {
                _watcher?.Dispose();
                _watcher = null;
            }
            base.Dispose(disposing);
        }
    }
}
