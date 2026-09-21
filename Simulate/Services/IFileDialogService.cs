using System;

namespace Simulate.Services
{
    public interface IFileDialogService
    {
        string? OpenFileDialog(string filter, string title);
        string? SaveFileDialog(string filter, string title, string? defaultFileName = null);
    }

    public sealed class DefaultFileDialogService : IFileDialogService
    {
        public string? OpenFileDialog(string filter, string title)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? SaveFileDialog(string filter, string title, string? defaultFileName = null)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName ?? string.Empty
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
