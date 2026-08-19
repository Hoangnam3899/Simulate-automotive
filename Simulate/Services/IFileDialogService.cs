using System;

namespace Simulate.Services
{
    public interface IFileDialogService
    {
        string? OpenFileDialog(string filter, string title);
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
    }
}
