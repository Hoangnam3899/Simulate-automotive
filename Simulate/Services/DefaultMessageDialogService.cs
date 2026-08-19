using System;
using System.Collections.Generic;
using System.Windows;
using Simulate.Models;
using Simulate.Views;

namespace Simulate.Services
{
    public sealed class DefaultMessageDialogService : IMessageDialogService
    {
        public IReadOnlyList<DbcMessage>? SelectMessages(DbcDocument document, IReadOnlyList<DbcMessage> alreadyAddedMessages)
        {
            ArgumentNullException.ThrowIfNull(document);

            var window = new SelectMessageWindow(document, alreadyAddedMessages)
            {
                Owner = Application.Current?.MainWindow
            };

            return window.ShowDialog() == true ? window.SelectedMessages : null;
        }
    }
}
