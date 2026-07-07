using System;
using System.Windows.Forms;

namespace PropertiesSearch
{
    /// <summary>
    /// Owns the filter state for one PropertyGrid: caches the shell's real
    /// (unproxied) selection, and swaps FilteredComponent proxies in and out as
    /// the query changes. When the query is empty the grid always holds the
    /// original objects, so stock behavior is completely untouched.
    /// </summary>
    internal sealed class FilterController : IDisposable
    {
        private const int DebounceMs = 200;

        private readonly PropertyGrid _grid;
        private readonly Timer _debounce = new Timer { Interval = DebounceMs };

        private object[] _originals = new object[0];
        private string   _query     = string.Empty;
        private bool     _applying;
        private bool     _proxied;

        internal FilterController(PropertyGrid grid)
        {
            _grid = grid;
            _grid.SelectedObjectsChanged += OnSelectedObjectsChanged;
            _debounce.Tick += (s, e) => { _debounce.Stop(); Apply(); };

            _originals = _grid.SelectedObjects ?? new object[0];
        }

        internal void SetQuery(string query)
        {
            _query = query?.Trim() ?? string.Empty;
            _debounce.Stop();
            _debounce.Start();
        }

        // The shell (or the user, via the object dropdown / designer) pushed a new
        // selection. Capture it as the new originals and re-apply the filter.
        private void OnSelectedObjectsChanged(object sender, EventArgs e)
        {
            if (_applying) return;

            var objs = _grid.SelectedObjects ?? new object[0];

            // Ignore notifications for our own proxies (defensive; _applying
            // already guards the synchronous case).
            foreach (var o in objs)
                if (o is FilteredComponent) return;

            _originals = objs;
            _proxied   = false;

            if (_query.Length > 0) Apply();
        }

        private void Apply()
        {
            if (_grid.IsDisposed) return;

            // Nothing to do: no filter active and the grid already holds originals.
            if (_query.Length == 0 && !_proxied) return;

            _applying = true;
            try
            {
                if (_query.Length == 0)
                {
                    _grid.SelectedObjects = _originals;
                    _proxied = false;
                }
                else
                {
                    var proxies = new object[_originals.Length];
                    for (int i = 0; i < _originals.Length; i++)
                        proxies[i] = new FilteredComponent(_originals[i], _query);
                    _grid.SelectedObjects = proxies;
                    _proxied = true;
                }
                _grid.Refresh();
            }
            finally { _applying = false; }
        }

        /// <summary>Puts the original objects back (used before shutdown).</summary>
        internal void Restore()
        {
            if (_grid.IsDisposed || !_proxied) return;
            _applying = true;
            try
            {
                _grid.SelectedObjects = _originals;
                _proxied = false;
            }
            finally { _applying = false; }
        }

        public void Dispose()
        {
            _debounce.Stop();
            _debounce.Dispose();
            if (!_grid.IsDisposed)
                _grid.SelectedObjectsChanged -= OnSelectedObjectsChanged;
            Restore();
        }
    }
}
