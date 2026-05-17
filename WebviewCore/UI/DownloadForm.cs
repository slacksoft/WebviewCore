namespace WebviewCore;

public partial class DownloadForm : Form
{
    private readonly byte[] _data;
    private readonly string _filename;

    public DownloadForm(FetchResult result)
    {
        _data = result.Data ?? Array.Empty<byte>();
        _filename = result.DownloadFilename ?? "download";
        Text = "Download File";
        ClientSize = new Size(420, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var contentTypes = string.IsNullOrEmpty(result.ContentType) ? "Unknown" : result.ContentType;
        var size = FormatSize(_data.Length);

        var lblInfo = new Label
        {
            Text = $"File: {_filename}\nType: {contentTypes}\nSize: {size}",
            Location = new Point(12, 12),
            AutoSize = false,
            Size = new Size(396, 60),
        };

        var btnSave = new Button
        {
            Text = "Save...",
            Location = new Point(120, 90),
            Size = new Size(90, 30),
        };
        btnSave.Click += BtnSave_Click;

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(220, 90),
            Size = new Size(90, 30),
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(lblInfo);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            FileName = _filename,
            Filter = "All Files|*.*",
        };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllBytes(sfd.FileName, _data);
            MessageBox.Show($"File saved to:\n{sfd.FileName}", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        Close();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
