namespace WebviewCore;

public partial class MainForm : Form
{
    private readonly TabControl _tabControl;
    private readonly Button _addTabBtn;
    public MainForm()
    {
        Text = "WebviewCore";
        ClientSize = new Size(960, 720);
        BackColor = Color.White;
        DoubleBuffered = true;

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabControl.DrawItem += TabControl_DrawItem!;
        _tabControl.MouseDown += TabControl_MouseDown!;
        _tabControl.Padding = new Point(20, 3);
        _tabControl.SelectedIndexChanged += (_, _) => UpdateTitleFromCurrentTab();

        _addTabBtn = new Button
        {
            Text = "+",
            Size = new Size(24, 22),
            FlatStyle = FlatStyle.Popup,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(ClientSize.Width - 32, 3),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _addTabBtn.BringToFront();
        _addTabBtn.Click += (_, _) => AddNewTab("about:blank");
        Resize += (_, _) => { _addTabBtn.Location = new Point(ClientSize.Width - 32, 3); };

        Controls.Add(_tabControl);
        Controls.Add(_addTabBtn);
        Shown += (_, _) => AddNewTab("about:blank");
    }

    private void AddNewTab(string url)
    {
        var browser = new BrowserControl();
        browser.TitleChanged += (_, title) =>
        {
            var page = FindPageForBrowser(browser);
            if (page != null)
            {
                page.Text = string.IsNullOrEmpty(title) || title == "WebviewCore" ? "New Tab" : title;
                if (_tabControl.SelectedTab == page)
                    Text = title + " - WebviewCore";
            }
        };
        browser.NewTabRequested += (_, newUrl) => AddNewTab(newUrl);
        browser.CloseRequested += (_, _) =>
        {
            var page = FindPageForBrowser(browser);
            if (page != null) CloseTab(page);
        };
        browser.PrintRequested += (_, _) =>
        {
            browser.BeginInvoke(() =>
            {
                using var pd = new PrintDialog();
                pd.ShowDialog();
            });
        };
        browser.DownloadRequested += (_, result) =>
        {
            browser.BeginInvoke(() =>
            {
                using var df = new DownloadForm(result);
                df.ShowDialog(browser.Parent);
            });
        };

        var tabPage = new TabPage("New Tab");
        browser.Dock = DockStyle.Fill;
        tabPage.Controls.Add(browser);
        _tabControl.TabPages.Add(tabPage);
        _tabControl.SelectedTab = tabPage;

        _ = browser.LoadPageAsync(url);
    }

    private void CloseTab(TabPage page)
    {
        if (_tabControl.TabPages.Count <= 1)
        {
            AddNewTab("about:blank");
            _tabControl.TabPages.Remove(page);
            page.Dispose();
            _tabControl.SelectedIndex = 0;
        }
        else
        {
            var idx = _tabControl.TabPages.IndexOf(page);
            _tabControl.TabPages.Remove(page);
            page.Dispose();
            if (idx >= _tabControl.TabPages.Count)
                idx = _tabControl.TabPages.Count - 1;
            _tabControl.SelectedIndex = idx;
        }
    }

    private TabPage? FindPageForBrowser(BrowserControl browser)
    {
        foreach (TabPage page in _tabControl.TabPages)
            if (page.Controls.Contains(browser))
                return page;
        return null;
    }

    private void UpdateTitleFromCurrentTab()
    {
        var page = _tabControl.SelectedTab;
        if (page != null && page.Controls.Count > 0 && page.Controls[0] is BrowserControl bc)
            Text = (bc.Title ?? "WebviewCore") + " - WebviewCore";
        else
            Text = "WebviewCore";
    }

    private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var r = e.Bounds;
        var idx = e.Index;

        if (idx < 0 || idx >= _tabControl.TabPages.Count) return;
        var page = _tabControl.TabPages[idx];

        var backColor = idx == _tabControl.SelectedIndex ? SystemColors.Window : SystemColors.Control;
        using (var b = new SolidBrush(backColor))
            g.FillRectangle(b, r);

        var textColor = idx == _tabControl.SelectedIndex ? SystemColors.ControlText : SystemColors.GrayText;
        TextRenderer.DrawText(g, page.Text, page.Font, new Point(r.X + 4, r.Y + 4), textColor);
    }

    private void TabControl_MouseDown(object sender, MouseEventArgs e)
    {
        var tc = (TabControl)sender;
        for (int i = 0; i < tc.TabPages.Count; i++)
        {
            var r = tc.GetTabRect(i);
            var closeRect = new Rectangle(r.Right - 18, r.Top + 4, 14, 14);
            if (closeRect.Contains(e.Location))
            {
                CloseTab(tc.TabPages[i]);
                return;
            }
        }
    }
}
