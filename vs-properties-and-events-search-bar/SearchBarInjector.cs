using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PropertiesSearch
{
    /// <summary>
    /// Mounts a themed search TextBox above the Properties window grid and keeps
    /// it there: whenever the shell lays the grid out to fill the pane, the grid
    /// is nudged down by the bar's height and the bar takes the freed strip.
    /// </summary>
    internal sealed class SearchBarInjector : IDisposable
    {
        private const int BarPad = 3;   // padding around the textbox inside the bar

        private readonly PropertyGrid _grid;
        private readonly Panel   _bar;        // themed background strip
        private readonly Panel   _border;     // 1px border around the textbox
        private readonly TextBox _textBox;
        private readonly FilterController _controller;

        private bool _adjusting;
        private bool _disposed;

        /// <summary>Raised when the grid's handle is destroyed (pane recreated).</summary>
        public event EventHandler GridGone;

        internal SearchBarInjector(PropertyGrid grid)
        {
            _grid = grid;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 9f),
            };
            SendMessage(_textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, "Search properties & events");
            _textBox.KeyDown += OnTextBoxKeyDown;

            _border = new Panel { Padding = new Padding(1) };
            _border.Controls.Add(_textBox);
            _textBox.Dock = DockStyle.Fill;
            _textBox.Multiline = false;

            _bar = new Panel { Height = _textBox.PreferredHeight + 2 + BarPad * 2 };
            _border.Height = _textBox.PreferredHeight + 2;
            _bar.Controls.Add(_border);
            _bar.Resize += (s, e) => LayoutBar();

            ApplyTheme();
            VSColorTheme.ThemeChanged += OnThemeChanged;

            Mount();

            _controller = new FilterController(_grid);
            _textBox.TextChanged += (s, e) => _controller.SetQuery(_textBox.Text);

            _grid.LocationChanged += OnGridBoundsChanged;
            _grid.SizeChanged     += OnGridBoundsChanged;
            _grid.HandleDestroyed += OnGridHandleDestroyed;

            AdjustNow();
        }

        // ── Mounting / geometry ────────────────────────────────────────────

        private void Mount()
        {
            if (_grid.Parent != null)
            {
                DiagLog.Write($"Mounting into managed parent ({_grid.Parent.GetType().FullName}).");
                _grid.Parent.Controls.Add(_bar);
                _bar.BringToFront();
            }
            else
            {
                // The grid hangs off a native pane window: parent our bar's HWND
                // next to the grid's. The bar stays a functional WinForms control.
                IntPtr paneHwnd = GetAncestor(_grid.Handle, GA_PARENT);
                DiagLog.Write($"Mounting next to native parent 0x{(long)paneHwnd:X}.");
                if (paneHwnd != IntPtr.Zero)
                {
                    _bar.CreateControl();               // realize the handle (parked)
                    SetParent(_bar.Handle, paneHwnd);
                    ShowWindow(_bar.Handle, SW_SHOWNA); // parked controls stay unmapped otherwise
                }
            }
        }

        private void LayoutBar()
        {
            _border.SetBounds(BarPad, BarPad, _bar.Width - BarPad * 2, _bar.Height - BarPad * 2);
        }

        private void OnGridBoundsChanged(object sender, EventArgs e) => AdjustNow();

        private void AdjustNow()
        {
            if (_adjusting || _disposed) return;
            _adjusting = true;
            try
            {
                int barH = _bar.Height;
                var b = _grid.Bounds;

                // The shell lays the grid out at the pane's top; reclaim the strip.
                if (b.Y < barH)
                {
                    _grid.SetBounds(b.X, barH, b.Width, Math.Max(0, b.Height - (barH - b.Y)));
                    b = _grid.Bounds;
                }

                _bar.SetBounds(b.X, b.Y - barH, b.Width, barH);
                LayoutBar();
            }
            finally { _adjusting = false; }
        }

        private void OnGridHandleDestroyed(object sender, EventArgs e)
        {
            GridGone?.Invoke(this, EventArgs.Empty);
        }

        // ── Interaction / theming ──────────────────────────────────────────

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _textBox.Clear();
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void OnThemeChanged(ThemeChangedEventArgs e) => ApplyTheme();

        private void ApplyTheme()
        {
            Color back   = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            Color fore   = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            Color border = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey);

            _bar.BackColor      = back;
            _border.BackColor   = border;
            _textBox.BackColor  = back;
            _textBox.ForeColor  = fore;
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            VSColorTheme.ThemeChanged -= OnThemeChanged;

            if (!_grid.IsDisposed)
            {
                _grid.LocationChanged -= OnGridBoundsChanged;
                _grid.SizeChanged     -= OnGridBoundsChanged;
                _grid.HandleDestroyed -= OnGridHandleDestroyed;
            }

            _controller.Dispose();
            _bar.Parent?.Controls.Remove(_bar);
            _bar.Dispose();
        }

        // ── Win32 ──────────────────────────────────────────────────────────

        private const int EM_SETCUEBANNER = 0x1501;
        private const uint GA_PARENT = 1;
        private const int SW_SHOWNA = 8;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, string lparam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    }
}
