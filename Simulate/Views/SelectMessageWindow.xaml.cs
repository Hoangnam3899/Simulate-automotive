using System;
using System.Collections.Generic;
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

        public sealed class SelectableDbcSignal : INotifyPropertyChanged
        {
            private bool _isSelected;

            public event PropertyChangedEventHandler? PropertyChanged;

            public DbcSignal Signal { get; }
            public DbcMessage ParentMessage { get; }

            public string Name => Signal.Name;
            public string ParentMessageName => ParentMessage.Name;
            public string FormattedId => ParentMessage.IsExtendedIdentifier
                ? $"0x{ParentMessage.Identifier:X8}"
                : $"0x{ParentMessage.Identifier:X3}";

            public int StartBit => Signal.StartBit;
            public int BitLength => Signal.BitLength;
            public string Unit => string.IsNullOrEmpty(Signal.Unit) ? "-" : Signal.Unit;
            public string Transmitter => ParentMessage.Transmitter;

            public string TooltipSummary =>
                $"Signal: {Name}\n" +
                $"Parent Message: {ParentMessageName} ({FormattedId})\n" +
                $"StartBit: {StartBit}, BitLength: {BitLength} bits\n" +
                $"Range: [{Signal.Minimum} .. {Signal.Maximum}] {Unit}\n" +
                $"Factor: {Signal.Factor}, Offset: {Signal.Offset}";

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

            public SelectableDbcSignal(DbcSignal signal, DbcMessage parentMessage, bool isSelected = false)
            {
                Signal = signal ?? throw new ArgumentNullException(nameof(signal));
                ParentMessage = parentMessage ?? throw new ArgumentNullException(nameof(parentMessage));
                _isSelected = isSelected;
            }
        }

        private readonly List<SelectableDbcMessage> _allMessages;
        private readonly List<SelectableDbcSignal> _allSignals;

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

            _allSignals = _allMessages
                .SelectMany(m => m.Message.Signals.Select(s =>
                {
                    var sigItem = new SelectableDbcSignal(s, m.Message, false);
                    sigItem.PropertyChanged += Item_PropertyChanged;
                    return sigItem;
                }))
                .ToList();

            DgMessages.ItemsSource = _allMessages;
            DgSignals.ItemsSource = _allSignals;

            MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(this).Y < 35)
                {
                    DragMove();
                }
            };

            UpdateSearchHint();
            UpdateStatus();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableDbcMessage.IsSelected) ||
                e.PropertyName == nameof(SelectableDbcSignal.IsSelected))
            {
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            bool isBySignal = TglSearchBySignal?.IsChecked == true;
            if (isBySignal)
            {
                int selectedSignals = _allSignals.Count(s => s.IsSelected);
                int distinctParentMessages = _allSignals
                    .Where(s => s.IsSelected)
                    .Select(s => s.ParentMessage.Identifier)
                    .Distinct()
                    .Count();
                TxtStatus.Text = $"{selectedSignals} of {_allSignals.Count} signals selected ({distinctParentMessages} messages)";
            }
            else
            {
                int selectedCount = _allMessages.Count(m => m.IsSelected);
                TxtStatus.Text = $"{selectedCount} of {_allMessages.Count} messages selected";
            }
        }

        private void UpdateSearchHint()
        {
            if (TxtSearchHint == null) return;
            string key = (TglSearchBySignal?.IsChecked == true)
                ? "Loc_SelMsg_SearchHint_WithSignals"
                : "Loc_SelMsg_SearchHint";

            if (TryFindResource(key) is string hintText && !string.IsNullOrEmpty(hintText))
            {
                TxtSearchHint.Text = hintText;
            }
        }

        private void TglSearchBySignal_Click(object sender, RoutedEventArgs e)
        {
            bool isBySignal = TglSearchBySignal?.IsChecked == true;

            if (isBySignal)
            {
                DgMessages.Visibility = Visibility.Collapsed;
                DgSignals.Visibility = Visibility.Visible;
            }
            else
            {
                DgSignals.Visibility = Visibility.Collapsed;
                DgMessages.Visibility = Visibility.Visible;
            }

            UpdateSearchHint();
            ApplyFilter();
            UpdateStatus();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = TxtSearch.Text.Trim();
            bool isBySignal = TglSearchBySignal?.IsChecked == true;

            if (isBySignal)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    DgSignals.ItemsSource = _allSignals;
                }
                else
                {
                    DgSignals.ItemsSource = _allSignals
                        .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    s.ParentMessageName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    s.FormattedId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    s.Transmitter.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    DgMessages.ItemsSource = _allMessages;
                }
                else
                {
                    DgMessages.ItemsSource = _allMessages
                        .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    m.FormattedId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    m.Transmitter.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool isBySignal = TglSearchBySignal?.IsChecked == true;
            if (isBySignal)
            {
                if (DgSignals.ItemsSource is IEnumerable<SelectableDbcSignal> visibleSignals)
                {
                    foreach (var sig in visibleSignals)
                    {
                        sig.IsSelected = true;
                    }
                }
            }
            else
            {
                if (DgMessages.ItemsSource is IEnumerable<SelectableDbcMessage> visibleMessages)
                {
                    foreach (var message in visibleMessages)
                    {
                        message.IsSelected = true;
                    }
                }
            }
            UpdateStatus();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            bool isBySignal = TglSearchBySignal?.IsChecked == true;
            if (isBySignal)
            {
                if (DgSignals.ItemsSource is IEnumerable<SelectableDbcSignal> visibleSignals)
                {
                    foreach (var sig in visibleSignals)
                    {
                        sig.IsSelected = false;
                    }
                }
            }
            else
            {
                if (DgMessages.ItemsSource is IEnumerable<SelectableDbcMessage> visibleMessages)
                {
                    foreach (var message in visibleMessages)
                    {
                        message.IsSelected = false;
                    }
                }
            }
            UpdateStatus();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Gom tất cả Message cha từ các Signal được tick chọn
            var parentMessagesFromSignals = _allSignals
                .Where(s => s.IsSelected)
                .Select(s => s.ParentMessage);

            // Kết hợp với các Message được tick chọn trực tiếp từ chế độ Message
            var directMessages = _allMessages
                .Where(m => m.IsSelected)
                .Select(m => m.Message);

            SelectedMessages = parentMessagesFromSignals
                .Concat(directMessages)
                .GroupBy(m => (m.Identifier, m.IsExtendedIdentifier))
                .Select(g => g.First())
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
