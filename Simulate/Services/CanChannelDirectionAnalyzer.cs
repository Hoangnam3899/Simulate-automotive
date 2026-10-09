using System;
using System.Collections.Generic;
using System.Linq;
using Simulate.Models;

namespace Simulate.Services
{
    public enum ChannelDirectionStatus
    {
        Unknown,
        Normal,
        SuspectedInverted
    }

    public sealed class DirectionEvaluationResult
    {
        public ChannelDirectionStatus Status { get; }
        public string TransmitterNode { get; }
        public string SampleMessageName { get; }
        public uint SampleCanId { get; }
        public int RxCount { get; }
        public int TxCount { get; }
        public string Details { get; }

        public DirectionEvaluationResult(
            ChannelDirectionStatus status,
            string transmitterNode,
            string sampleMessageName,
            uint sampleCanId,
            int rxCount,
            int txCount,
            string details)
        {
            Status = status;
            TransmitterNode = transmitterNode;
            SampleMessageName = sampleMessageName;
            SampleCanId = sampleCanId;
            RxCount = rxCount;
            TxCount = txCount;
            Details = details;
        }
    }

    /// <summary>
    /// Analyzes the actual physical flow of CAN frames in real-time to detect if the TX and RX channels
    /// were connected in reverse relative to the loaded DBC transmitters.
    /// Supports multi-cycle connect/disconnect idempotent lifecycles without memory leaks.
    /// </summary>
    public sealed class CanChannelDirectionAnalyzer
    {
        private readonly object _sync = new();
        private DbcDocument? _currentDocument;
        private readonly Dictionary<(uint Identifier, bool IsExtended), DbcMessage> _messageLookup = new();
        private readonly Dictionary<string, (int RxCount, int TxCount, DbcMessage LastMsg)> _transmitterStats = new(StringComparer.OrdinalIgnoreCase);
        private int _totalProcessedFrames;
        private bool _isSampling;
        private bool _isEvaluated;
        private ChannelDirectionStatus _currentStatus = ChannelDirectionStatus.Unknown;

        public const int MinimumSampleThreshold = 15;
        public const int MaximumSampleFrames = 60;

        public ChannelDirectionStatus CurrentStatus
        {
            get
            {
                lock (_sync) return _currentStatus;
            }
        }

        public bool IsSampling
        {
            get
            {
                lock (_sync) return _isSampling;
            }
        }

        public bool IsEvaluated
        {
            get
            {
                lock (_sync) return _isEvaluated;
            }
        }

        private DirectionEvaluationResult? _lastResult;

        public DirectionEvaluationResult? LastResult
        {
            get
            {
                lock (_sync) return _lastResult;
            }
        }

        public event Action<DirectionEvaluationResult>? DirectionEvaluated;
        public event Action? DirectionReset;

        public void StartSampling(DbcDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            lock (_sync)
            {
                _currentDocument = document;
                _messageLookup.Clear();
                foreach (DbcMessage msg in document.Messages)
                {
                    _messageLookup[(msg.Identifier, msg.IsExtendedIdentifier)] = msg;
                }

                _transmitterStats.Clear();
                _totalProcessedFrames = 0;
                _isSampling = true;
                _isEvaluated = false;
                _currentStatus = ChannelDirectionStatus.Unknown;
            }
        }

        public void ProcessFrame(RoutedCanFrame routedFrame)
        {
            if (routedFrame == null) return;

            DirectionEvaluationResult? resultToFire = null;

            lock (_sync)
            {
                if (!_isSampling || _isEvaluated || _currentDocument == null)
                {
                    return;
                }

                var key = (routedFrame.Frame.Identifier, routedFrame.Frame.IsExtendedIdentifier);
                if (_messageLookup.TryGetValue(key, out DbcMessage? message))
                {
                    string transmitter = message.Transmitter;
                    if (!string.IsNullOrWhiteSpace(transmitter) && !string.Equals(transmitter, "Vector__XXX", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_transmitterStats.TryGetValue(transmitter, out var stats))
                        {
                            stats = (0, 0, message);
                        }

                        if (routedFrame.Source == CanGatewaySide.Rx)
                        {
                            stats.RxCount++;
                        }
                        else if (routedFrame.Source == CanGatewaySide.Tx)
                        {
                            stats.TxCount++;
                        }

                        stats.LastMsg = message;
                        _transmitterStats[transmitter] = stats;
                        _totalProcessedFrames++;

                        // Evaluate when dominant transmitter has enough samples or max frames reached
                        int dominantTotal = stats.RxCount + stats.TxCount;
                        if (dominantTotal >= MinimumSampleThreshold || _totalProcessedFrames >= MaximumSampleFrames)
                        {
                            // Find the most active transmitter
                            var topTransmitter = _transmitterStats
                                .OrderByDescending(kv => kv.Value.RxCount + kv.Value.TxCount)
                                .First();

                            string topNode = topTransmitter.Key;
                            int rxCount = topTransmitter.Value.RxCount;
                            int txCount = topTransmitter.Value.TxCount;
                            DbcMessage sampleMsg = topTransmitter.Value.LastMsg;

                            ChannelDirectionStatus evaluatedStatus;
                            string details;

                            if (txCount > rxCount && txCount >= 5)
                            {
                                evaluatedStatus = ChannelDirectionStatus.SuspectedInverted;
                                details = $"Cảnh báo chiều kết nối: Bản tin [{sampleMsg.Name} (0x{sampleMsg.Identifier:X})] của [{topNode}] xuất hiện ở Kênh TX thay vì Kênh RX ({txCount}/{rxCount + txCount} frames). Nghi vấn cắm ngược dây. Hãy kiểm tra lại kết nối phần cứng.";
                            }
                            else if (rxCount >= txCount && rxCount >= 5)
                            {
                                evaluatedStatus = ChannelDirectionStatus.Normal;
                                details = $"Xác nhận chiều kết nối: Kênh RX nhận đúng các bản tin của [{topNode}] ({rxCount}/{rxCount + txCount} frames).";
                            }
                            else
                            {
                                evaluatedStatus = ChannelDirectionStatus.Normal;
                                details = "Lưu lượng hai chiều hoạt động bình thường.";
                            }

                            _currentStatus = evaluatedStatus;
                            _isEvaluated = true;
                            _isSampling = false;

                            resultToFire = new DirectionEvaluationResult(
                                evaluatedStatus,
                                topNode,
                                sampleMsg.Name,
                                sampleMsg.Identifier,
                                rxCount,
                                txCount,
                                details);
                            _lastResult = resultToFire;
                        }
                    }
                }
            }

            if (resultToFire != null)
            {
                DirectionEvaluated?.Invoke(resultToFire);
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _transmitterStats.Clear();
                _totalProcessedFrames = 0;
                _isSampling = false;
                _isEvaluated = false;
                _currentStatus = ChannelDirectionStatus.Unknown;
                _lastResult = null;
            }
            DirectionReset?.Invoke();
        }
    }
}
