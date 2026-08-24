using System;
using System.Drawing;
using System.Windows.Forms;

namespace PKHeX.WinForms;

/// <summary>
/// Scrollable, resizable, copyable read-only text report. Replaces <see cref="WinFormsUtil.Alert"/> for output
/// that can run to hundreds of lines -- a MessageBox grows past the screen and silently clips instead of
/// scrolling, which made large reports (e.g. a save with 100+ cloned Pokémon) unreadable.
/// </summary>
/// <remarks>
/// Optionally shows a single action button; the dialog then returns <see cref="DialogResult.Yes"/> when it is
/// pressed, so a caller can offer "here is the report, do you want me to act on it?" in one window rather than
/// an alert followed by a separate prompt.
/// </remarks>
public sealed class ReportViewer : Form
{
    private readonly TextBox TB_Report;

    /// <param name="title">Window caption.</param>
    /// <param name="header">Short summary line shown above the scrollable body.</param>
    /// <param name="body">The report itself. Displayed monospaced so indented/aligned lines stay aligned.</param>
    /// <param name="actionText">Text for the optional action button. Null hides it.</param>
    public ReportViewer(string title, string header, string body, string? actionText = null)
    {
        Text = title;
        Icon = Properties.Resources.Icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(480, 320);

        var headerLabel = new Label
        {
            Text = header,
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 34,
            Padding = new Padding(10, 9, 10, 0),
        };

        TB_Report = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            // WordWrap off + both scrollbars: the report's indentation carries meaning (a cluster header with
            // its member slots underneath), and wrapping long slot identifiers would destroy that structure.
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            Text = body,
            BackColor = SystemColors.Window,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 42,
            Padding = new Padding(6),
        };

        var close = new Button { Text = "Close", Size = new Size(90, 27), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(close);

        if (actionText is not null)
        {
            var action = new Button { Text = actionText, Size = new Size(190, 27), DialogResult = DialogResult.Yes };
            buttons.Controls.Add(action);
            AcceptButton = action;
        }

        var copy = new Button { Text = "Copy to Clipboard", Size = new Size(130, 27) };
        copy.Click += (_, _) => WinFormsUtil.SetClipboardText(body);
        buttons.Controls.Add(copy);

        CancelButton = close;

        // Add in reverse z-order so Fill takes the space left over by the docked header/footer.
        Controls.Add(TB_Report);
        Controls.Add(headerLabel);
        Controls.Add(buttons);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Land at the top of the report with nothing highlighted; a TextBox otherwise opens fully selected.
        TB_Report.SelectionStart = 0;
        TB_Report.SelectionLength = 0;
    }
}
