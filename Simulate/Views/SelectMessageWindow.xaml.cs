using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Simulate.Models;

namespace Simulate.Views
{
    public partial class SelectMessageWindow : Window
    {
        public sealed class SelectableDbcMessage : INotifyPropertyChanged
        {
            private bool _isSelected;

            public event PropertyChangedEventHandler? PropertyChanged;

            public DbcMessage Message { get; }

            public string FormattedId => Message.IsExtendedIdentifier
                ? $"0x{Message.Identifier:X8}"
                : $"0x{Message.Identifier:X3}";

            public string Name => Message.Name;

            public int PayloadLength => Message.PayloadLength;

            public int SignalCount => Message.Signals.Count;

            public string Transmitter => Message.Transmitter;

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public SelectableDbcMessage(DbcMessage message, bool isSelected = false)
            {
                Message = message ?? throw new ArgumentNullException(nameof(message));
                _isSelected = isSelected;
            }
        }

        private readonly List<SelectableDbcMessage> _allMessages;
        private readonly ObservableCollection<SelectableDbcMessage> _filteredMessages;

        public IReadOnlyList<DbcMessage> SelectedMessages { get; private set; } = Array.Empty<DbcMessage>();

        public SelectMessageWindow(DbcDocument document, IReadOnlyList<DbcMessage>? alreadyAddedMessages = null)
        {
            InitializeComponent();
            ArgumentNullException.ThrowIfNull(document);

            var alreadyAddedIds = new HashSet<(uint, bool)>(
                (alreadyAddedMessages ?? Array.Empty<DbcMessage>())
                .Select(m => (m.Identifier, m.IsExtendedIdentifier)));

            _allMessages = document.Messages
                .Select(m =>
                {
                    var item = new SelectableDbcMessage(m, false);
                    item.PropertyChanged += Item_PropertyChanged;
                    return item;
                })
                .ToList();

            _filteredMessages = new ObservableCollection<SelectableDbcMessage>(_allMessages);
            DgMessages.ItemsSource = _filteredMessages;

            MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(this).Y < 35)
                {
                    DragMove();
                }
            };

            UpdateStatus();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableDbcMessage.IsSelected))
            {
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            int selectedCount = _allMessages.Count(m => m.IsSelected);
            TxtStatus.Text = $"{selectedCount} of {_allMessages.Count} messages selected";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text.Trim();
            _filteredMessages.Clear();

            var matches = string.IsNullOrWhiteSpace(query)
                ? _allMessages
                : _allMessages.Where(m =>
                    m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.FormattedId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.Transmitter.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var match in matches)
            {
                _filteredMessages.Add(match);
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var message in _filteredMessages)
            {
                message.IsSelected = true;
            }
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var message in _filteredMessages)
            {
                message.IsSelected = false;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            SelectedMessages = _allMessages
                .Where(m => m.IsSelected)
                .Select(m => m.Message)
                .ToList();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
