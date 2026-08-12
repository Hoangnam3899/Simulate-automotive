using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Simulate.Models
{
    public enum DbcByteOrder
    {
        BigEndian,
        LittleEndian
    }

    public enum DbcParseIssueSeverity
    {
        Warning,
        Error
    }

    public enum DbcParseIssueCode
    {
        InvalidMessageDefinition,
        InvalidMessageIdentifier,
        InvalidPayloadLength,
        InvalidSignalDefinition,
        InvalidSignalLayout,
        SignalWithoutMessage,
        UnsupportedStatement,
        InvalidValueDescription
    }

    public sealed class DbcDocument
    {
        internal DbcDocument(IEnumerable<DbcNode> nodes, IEnumerable<DbcMessage> messages)
        {
            Nodes = CreateReadOnlyList(nodes, nameof(nodes));
            Messages = CreateReadOnlyList(messages, nameof(messages));
        }

        public IReadOnlyList<DbcNode> Nodes { get; }

        public IReadOnlyList<DbcMessage> Messages { get; }

        private static ReadOnlyCollection<T> CreateReadOnlyList<T>(IEnumerable<T> source, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(source, parameterName);
            return new ReadOnlyCollection<T>(source.ToArray());
        }
    }

    public sealed class DbcNode
    {
        internal DbcNode(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public sealed class DbcMessage
    {
        internal DbcMessage(
            string name,
            uint identifier,
            bool isExtendedIdentifier,
            int payloadLength,
            string transmitter,
            IEnumerable<DbcSignal> signals)
        {
            Name = name;
            Identifier = identifier;
            IsExtendedIdentifier = isExtendedIdentifier;
            PayloadLength = payloadLength;
            Transmitter = transmitter;
            Signals = CreateReadOnlyList(signals, nameof(signals));
        }

        public string Name { get; }

        public uint Identifier { get; }

        public bool IsExtendedIdentifier { get; }

        public int PayloadLength { get; }

        public string Transmitter { get; }

        public IReadOnlyList<DbcSignal> Signals { get; }

        private static ReadOnlyCollection<T> CreateReadOnlyList<T>(IEnumerable<T> source, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(source, parameterName);
            return new ReadOnlyCollection<T>(source.ToArray());
        }
    }

    public sealed class DbcSignal
    {
        internal DbcSignal(
            string name,
            int startBit,
            int bitLength,
            DbcByteOrder byteOrder,
            bool isSigned,
            double factor,
            double offset,
            double minimum,
            double maximum,
            string unit,
            IEnumerable<string> receivers,
            IEnumerable<DbcValueDescription> valueDescriptions)
        {
            Name = name;
            StartBit = startBit;
            BitLength = bitLength;
            ByteOrder = byteOrder;
            IsSigned = isSigned;
            Factor = factor;
            Offset = offset;
            Minimum = minimum;
            Maximum = maximum;
            Unit = unit;
            ArgumentNullException.ThrowIfNull(receivers);
            Receivers = new ReadOnlyCollection<string>(receivers.ToArray());
            ArgumentNullException.ThrowIfNull(valueDescriptions);
            ValueDescriptions = new ReadOnlyCollection<DbcValueDescription>(valueDescriptions.ToArray());
        }

        public string Name { get; }

        public int StartBit { get; }

        public int BitLength { get; }

        public DbcByteOrder ByteOrder { get; }

        public bool IsSigned { get; }

        public double Factor { get; }

        public double Offset { get; }

        public double Minimum { get; }

        public double Maximum { get; }

        public string Unit { get; }

        public IReadOnlyList<string> Receivers { get; }

        /// <summary>
        /// Gets the typed choices declared by a DBC <c>VAL_</c> statement.
        /// </summary>
        public IReadOnlyList<DbcValueDescription> ValueDescriptions { get; }
    }

    /// <summary>
    /// Represents one raw DBC value, its display label, and its mapped physical value.
    /// </summary>
    public sealed class DbcValueDescription
    {
        internal DbcValueDescription(long rawValue, double physicalValue, string description)
        {
            RawValue = rawValue;
            PhysicalValue = physicalValue;
            Description = description;
        }

        /// <summary>
        /// Gets the integer value encoded in the signal bit field.
        /// </summary>
        public long RawValue { get; }

        /// <summary>
        /// Gets the physical value after applying the signal factor and offset.
        /// </summary>
        public double PhysicalValue { get; }

        /// <summary>
        /// Gets the human-readable label declared by the DBC file.
        /// </summary>
        public string Description { get; }
    }

    public sealed class DbcParseIssue
    {
        internal DbcParseIssue(
            DbcParseIssueSeverity severity,
            DbcParseIssueCode code,
            int lineNumber,
            string context,
            string message)
        {
            Severity = severity;
            Code = code;
            LineNumber = lineNumber;
            Context = context;
            Message = message;
        }

        public DbcParseIssueSeverity Severity { get; }

        public DbcParseIssueCode Code { get; }

        public int LineNumber { get; }

        public string Context { get; }

        public string Message { get; }
    }

    public sealed class DbcParseResult
    {
        internal DbcParseResult(DbcDocument? document, IEnumerable<DbcParseIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(issues);

            Document = document;
            Issues = new ReadOnlyCollection<DbcParseIssue>(issues.ToArray());
            IsSuccess = Document is not null && Issues.All(issue => issue.Severity != DbcParseIssueSeverity.Error);
        }

        public DbcDocument? Document { get; }

        public IReadOnlyList<DbcParseIssue> Issues { get; }

        public bool IsSuccess { get; }
    }
}
