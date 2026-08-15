using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Simulate.Models;

namespace Simulate.Services
{
    public static class DbcParser
    {
        private const uint ExtendedIdentifierFlag = 0x80000000;
        private const uint ExtendedIdentifierMask = CanFrame.MaximumExtendedIdentifier;

        private static readonly Regex MessageExpression = new(
            @"^BO_\s+(?<identifier>\d+)\s+(?<name>\S+)\s*:\s*(?<payloadLength>\d+)\s+(?<transmitter>\S+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SignalExpression = new(
            @"^SG_\s+(?<name>\S+)(?:\s+(?<multiplexer>M|m\d+M?))?\s*:\s*(?<startBit>\d+)\|(?<bitLength>\d+)@(?<byteOrder>[01])(?<signed>[+-])\s+\((?<factor>[^,]+),(?<offset>[^\)]+)\)\s+\[(?<minimum>[^\|]+)\|(?<maximum>[^\]]+)\]\s+\""(?<unit>[^\""\r\n]*)\""\s*(?<receivers>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ValueDescriptionExpression = new(
            @"^VAL_\s+(?<identifier>\d+)\s+(?<signal>\S+)\s+(?<values>.*?)\s*;\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ValueDescriptionPairExpression = new(
            @"\G\s*(?<raw>[+-]?\d+)\s+""(?<description>(?:\\.|[^""\\])*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static DbcParseResult Parse(string documentText)
        {
            ArgumentNullException.ThrowIfNull(documentText);

            var nodes = new List<DbcNode>();
            var messages = new List<MutableDbcMessage>();
            var messagesByIdentity = new Dictionary<(uint Identifier, bool IsExtendedIdentifier), MutableDbcMessage>();
            var issues = new List<DbcParseIssue>();
            var reportedUnsupportedStatements = new HashSet<string>(StringComparer.Ordinal);
            MutableDbcMessage? currentMessage = null;
            string[] lines = documentText.Split('\n');

            for (int index = 0; index < lines.Length; index++)
            {
                string context = lines[index].TrimEnd('\r');
                string statement = context.Trim();
                int lineNumber = index + 1;

                if (statement.Length == 0)
                {
                    continue;
                }

                if (statement.StartsWith("VERSION", StringComparison.Ordinal))
                {
                    continue;
                }

                if (statement.StartsWith("BU_:", StringComparison.Ordinal))
                {
                    ParseNodes(statement, nodes);
                    continue;
                }

                if (IsStatementOfType(statement, "BO_"))
                {
                    currentMessage = ParseMessage(statement, lineNumber, context, issues);
                    if (currentMessage is not null)
                    {
                        (uint identifier, bool isExtendedIdentifier) =
                            (currentMessage.Identifier, currentMessage.IsExtendedIdentifier);
                        if (!messagesByIdentity.TryAdd((identifier, isExtendedIdentifier), currentMessage))
                        {
                            AddError(
                                issues,
                                DbcParseIssueCode.DuplicateMessageIdentifier,
                                lineNumber,
                                context,
                                "The BO_ statement duplicates a normalized CAN identifier and extended-identifier state.");
                        }
                        else
                        {
                            messages.Add(currentMessage);
                        }
                    }

                    continue;
                }

                if (IsStatementOfType(statement, "SG_"))
                {
                    ParseSignal(statement, currentMessage, lineNumber, context, issues);
                    continue;
                }

                if (IsStatementOfType(statement, "VAL_"))
                {
                    ParseValueDescriptions(statement, messagesByIdentity, lineNumber, context, issues);
                    continue;
                }

                ReportUnsupportedStatement(statement, lineNumber, context, issues, reportedUnsupportedStatements);
            }

            bool hasErrors = issues.Any(issue => issue.Severity == DbcParseIssueSeverity.Error);
            DbcDocument? document = hasErrors
                ? null
                : new DbcDocument(nodes, messages.Select(message => message.ToDocumentMessage()));

            return new DbcParseResult(document, issues);
        }

        private static void ParseNodes(string statement, List<DbcNode> nodes)
        {
            string declaredNodes = statement["BU_:".Length..].Trim();
            if (declaredNodes.Length == 0)
            {
                return;
            }

            foreach (string name in declaredNodes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                nodes.Add(new DbcNode(name));
            }
        }

        private static MutableDbcMessage? ParseMessage(
            string statement,
            int lineNumber,
            string context,
            List<DbcParseIssue> issues)
        {
            Match match = MessageExpression.Match(statement);
            if (!match.Success)
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidMessageDefinition,
                    lineNumber,
                    context,
                    "The BO_ statement does not match the supported DBC message syntax.");
                return null;
            }

            if (!uint.TryParse(
                    match.Groups["identifier"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out uint rawIdentifier)
                || !TryNormalizeIdentifier(rawIdentifier, out uint identifier, out bool isExtendedIdentifier))
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidMessageIdentifier,
                    lineNumber,
                    context,
                    "The DBC message identifier is not a valid standard or extended CAN identifier.");
                return null;
            }

            if (!int.TryParse(
                    match.Groups["payloadLength"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int payloadLength)
                || payloadLength < 0
                || payloadLength > CanFrame.MaximumPayloadLength)
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidPayloadLength,
                    lineNumber,
                    context,
                    $"The DBC payload length must be between 0 and {CanFrame.MaximumPayloadLength} bytes.");
                return null;
            }

            return new MutableDbcMessage(
                match.Groups["name"].Value,
                identifier,
                isExtendedIdentifier,
                payloadLength,
                match.Groups["transmitter"].Value);
        }

        private static void ParseSignal(
            string statement,
            MutableDbcMessage? currentMessage,
            int lineNumber,
            string context,
            List<DbcParseIssue> issues)
        {
            if (currentMessage is null)
            {
                AddError(
                    issues,
                    DbcParseIssueCode.SignalWithoutMessage,
                    lineNumber,
                    context,
                    "A SG_ statement must follow a valid BO_ message statement.");
                return;
            }

            Match match = SignalExpression.Match(statement);
            if (!match.Success)
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidSignalDefinition,
                    lineNumber,
                    context,
                    "The SG_ statement does not match the supported DBC signal syntax.");
                return;
            }

            if (match.Groups["multiplexer"].Success)
            {
                AddWarning(
                    issues,
                    DbcParseIssueCode.UnsupportedStatement,
                    lineNumber,
                    context,
                    "Multiplexed SG_ statements are not represented by the Task 7 DBC domain.");
                return;
            }

            if (!TryParseSignalNumbers(match, out int startBit, out int bitLength, out double factor, out double offset, out double minimum, out double maximum))
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidSignalDefinition,
                    lineNumber,
                    context,
                    "The SG_ statement contains an invalid integer or physical conversion value.");
                return;
            }

            DbcByteOrder byteOrder = match.Groups["byteOrder"].Value == "1"
                ? DbcByteOrder.LittleEndian
                : DbcByteOrder.BigEndian;

            if (!IsSignalLayoutWithinPayload(startBit, bitLength, byteOrder, currentMessage.PayloadLength)
                || minimum > maximum)
            {
                AddError(
                    issues,
                    DbcParseIssueCode.InvalidSignalLayout,
                    lineNumber,
                    context,
                    "The SG_ bit layout or physical range does not fit the containing CAN message.");
                return;
            }

            bool isSigned = match.Groups["signed"].Value == "-";
            string[] receivers = match.Groups["receivers"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var signal = new DbcSignal(
                match.Groups["name"].Value,
                startBit,
                bitLength,
                byteOrder,
                isSigned,
                factor,
                offset,
                minimum,
                maximum,
                match.Groups["unit"].Value,
                receivers,
                valueDescriptions: []);
            if (!currentMessage.TryAddSignal(signal))
            {
                AddError(
                    issues,
                    DbcParseIssueCode.DuplicateSignalName,
                    lineNumber,
                    context,
                    "The SG_ statement duplicates a signal name within its containing DBC message.");
            }
        }

        private static void ParseValueDescriptions(
            string statement,
            IReadOnlyDictionary<(uint Identifier, bool IsExtendedIdentifier), MutableDbcMessage> messagesByIdentity,
            int lineNumber,
            string context,
            List<DbcParseIssue> issues)
        {
            Match statementMatch = ValueDescriptionExpression.Match(statement);
            if (!statementMatch.Success
                || !uint.TryParse(
                    statementMatch.Groups["identifier"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out uint rawIdentifier)
                || !TryNormalizeIdentifier(rawIdentifier, out uint identifier, out bool isExtendedIdentifier))
            {
                AddWarning(
                    issues,
                    DbcParseIssueCode.InvalidValueDescription,
                    lineNumber,
                    context,
                    "The VAL_ statement does not match the supported DBC value-description syntax.");
                return;
            }

            messagesByIdentity.TryGetValue((identifier, isExtendedIdentifier), out MutableDbcMessage? message);
            DbcSignal? signal = message?.FindSignal(statementMatch.Groups["signal"].Value);
            if (signal is null)
            {
                AddWarning(
                    issues,
                    DbcParseIssueCode.InvalidValueDescription,
                    lineNumber,
                    context,
                    "The VAL_ statement does not reference a parsed DBC signal.");
                return;
            }

            string valuePairs = statementMatch.Groups["values"].Value;
            int position = 0;
            var parsedDescriptions = new List<DbcValueDescription>();
            while (position < valuePairs.Length)
            {
                Match pairMatch = ValueDescriptionPairExpression.Match(valuePairs, position);
                if (!pairMatch.Success
                    || pairMatch.Index != position
                    || !long.TryParse(
                        pairMatch.Groups["raw"].Value,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out long rawValue)
                    || !IsRawValueRepresentable(rawValue, signal)
                    || !TryMapRawToPhysical(rawValue, signal, out double physicalValue)
                    || parsedDescriptions.Any(description => description.RawValue == rawValue))
                {
                    AddWarning(
                        issues,
                        DbcParseIssueCode.InvalidValueDescription,
                        lineNumber,
                        context,
                        "The VAL_ statement contains an invalid or duplicate value-description pair.");
                    return;
                }

                parsedDescriptions.Add(new DbcValueDescription(
                    rawValue,
                    physicalValue,
                    UnescapeDescription(pairMatch.Groups["description"].Value)));
                position = pairMatch.Index + pairMatch.Length;
            }

            if (parsedDescriptions.Count == 0)
            {
                AddWarning(
                    issues,
                    DbcParseIssueCode.InvalidValueDescription,
                    lineNumber,
                    context,
                    "The VAL_ statement must declare at least one value-description pair.");
                return;
            }

            if (!message!.AddValueDescriptions(signal.Name, parsedDescriptions))
            {
                AddWarning(
                    issues,
                    DbcParseIssueCode.InvalidValueDescription,
                    lineNumber,
                    context,
                    "The VAL_ statement duplicates a raw value declared for the signal.");
            }
        }

        private static bool IsRawValueRepresentable(long rawValue, DbcSignal signal)
        {
            if (signal.IsSigned)
            {
                if (signal.BitLength == 64)
                {
                    return true;
                }

                long magnitude = 1L << (signal.BitLength - 1);
                return rawValue >= -magnitude && rawValue <= magnitude - 1;
            }

            if (rawValue < 0 || signal.BitLength >= 63)
            {
                return rawValue >= 0;
            }

            return rawValue <= (1L << signal.BitLength) - 1;
        }

        private static bool TryMapRawToPhysical(
            long rawValue,
            DbcSignal signal,
            out double physicalValue)
        {
            physicalValue = (rawValue * signal.Factor) + signal.Offset;
            return double.IsFinite(physicalValue);
        }

        private static string UnescapeDescription(string description)
        {
            return description
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        private static bool TryParseSignalNumbers(
            Match match,
            out int startBit,
            out int bitLength,
            out double factor,
            out double offset,
            out double minimum,
            out double maximum)
        {
            startBit = 0;
            bitLength = 0;
            factor = 0;
            offset = 0;
            minimum = 0;
            maximum = 0;

            bool isValid = int.TryParse(match.Groups["startBit"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out startBit)
                && int.TryParse(match.Groups["bitLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out bitLength)
                && TryParseFiniteDouble(match.Groups["factor"].Value, out factor)
                && TryParseFiniteDouble(match.Groups["offset"].Value, out offset)
                && TryParseFiniteDouble(match.Groups["minimum"].Value, out minimum)
                && TryParseFiniteDouble(match.Groups["maximum"].Value, out maximum);

            return isValid;
        }

        private static bool TryParseFiniteDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                && double.IsFinite(result);
        }

        private static bool IsSignalLayoutWithinPayload(
            int startBit,
            int bitLength,
            DbcByteOrder byteOrder,
            int payloadLength)
        {
            int payloadBitLength = payloadLength * 8;
            if (startBit < 0
                || startBit >= payloadBitLength
                || bitLength is < 1 or > 64)
            {
                return false;
            }

            if (byteOrder == DbcByteOrder.LittleEndian)
            {
                return startBit <= payloadBitLength - bitLength;
            }

            int currentBit = startBit;
            for (int index = 0; index < bitLength; index++)
            {
                if (currentBit < 0 || currentBit >= payloadBitLength)
                {
                    return false;
                }

                if (index < bitLength - 1)
                {
                    currentBit = currentBit % 8 == 0
                        ? currentBit + 15
                        : currentBit - 1;
                }
            }

            return true;
        }

        private static bool TryNormalizeIdentifier(
            uint rawIdentifier,
            out uint identifier,
            out bool isExtendedIdentifier)
        {
            isExtendedIdentifier = (rawIdentifier & ExtendedIdentifierFlag) != 0;
            if (!isExtendedIdentifier)
            {
                identifier = rawIdentifier;
                return identifier <= CanFrame.MaximumStandardIdentifier;
            }

            uint permittedBits = ExtendedIdentifierFlag | ExtendedIdentifierMask;
            if ((rawIdentifier & ~permittedBits) != 0)
            {
                identifier = 0;
                return false;
            }

            identifier = rawIdentifier & ExtendedIdentifierMask;
            return true;
        }

        private static void ReportUnsupportedStatement(
            string statement,
            int lineNumber,
            string context,
            List<DbcParseIssue> issues,
            HashSet<string> reportedUnsupportedStatements)
        {
            string statementName = GetStatementName(statement);
            if (!reportedUnsupportedStatements.Add(statementName))
            {
                return;
            }

            issues.Add(new DbcParseIssue(
                DbcParseIssueSeverity.Warning,
                DbcParseIssueCode.UnsupportedStatement,
                lineNumber,
                context,
                $"The DBC statement '{statementName}' is not represented by the Task 7 DBC domain."));
        }

        private static string GetStatementName(string statement)
        {
            int delimiterIndex = statement.IndexOfAny([' ', ':', '\t']);
            return delimiterIndex < 0 ? statement : statement[..delimiterIndex];
        }

        private static bool IsStatementOfType(string statement, string statementName)
        {
            return statement.StartsWith(statementName, StringComparison.Ordinal)
                && statement.Length > statementName.Length
                && char.IsWhiteSpace(statement[statementName.Length]);
        }

        private static void AddWarning(
            List<DbcParseIssue> issues,
            DbcParseIssueCode code,
            int lineNumber,
            string context,
            string message)
        {
            issues.Add(new DbcParseIssue(
                DbcParseIssueSeverity.Warning,
                code,
                lineNumber,
                context,
                message));
        }

        private static void AddError(
            List<DbcParseIssue> issues,
            DbcParseIssueCode code,
            int lineNumber,
            string context,
            string message)
        {
            issues.Add(new DbcParseIssue(
                DbcParseIssueSeverity.Error,
                code,
                lineNumber,
                context,
                message));
        }

        private sealed class MutableDbcMessage
        {
            private readonly List<DbcSignal> _signals = new();
            private readonly Dictionary<string, DbcSignal> _signalsByName = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<DbcValueDescription>> _valueDescriptions =
                new(StringComparer.Ordinal);

            public MutableDbcMessage(
                string name,
                uint identifier,
                bool isExtendedIdentifier,
                int payloadLength,
                string transmitter)
            {
                Name = name;
                Identifier = identifier;
                IsExtendedIdentifier = isExtendedIdentifier;
                PayloadLength = payloadLength;
                Transmitter = transmitter;
            }

            public string Name { get; }

            public uint Identifier { get; }

            public bool IsExtendedIdentifier { get; }

            public int PayloadLength { get; }

            public string Transmitter { get; }

            public bool TryAddSignal(DbcSignal signal)
            {
                if (!_signalsByName.TryAdd(signal.Name, signal))
                {
                    return false;
                }

                _signals.Add(signal);
                return true;
            }

            public DbcSignal? FindSignal(string signalName)
            {
                return _signalsByName.GetValueOrDefault(signalName);
            }

            public bool AddValueDescriptions(
                string signalName,
                IEnumerable<DbcValueDescription> valueDescriptions)
            {
                DbcValueDescription[] replacement = valueDescriptions.ToArray();
                if (!_valueDescriptions.TryGetValue(signalName, out List<DbcValueDescription>? descriptions))
                {
                    descriptions = new List<DbcValueDescription>();
                    _valueDescriptions.Add(signalName, descriptions);
                }

                if (replacement.Any(valueDescription =>
                    descriptions.Any(existing => existing.RawValue == valueDescription.RawValue)))
                {
                    return false;
                }

                descriptions.AddRange(replacement);
                return true;
            }

            public DbcMessage ToDocumentMessage()
            {
                return new DbcMessage(
                    Name,
                    Identifier,
                    IsExtendedIdentifier,
                    PayloadLength,
                    Transmitter,
                    _signals.Select(signal => new DbcSignal(
                        signal.Name,
                        signal.StartBit,
                        signal.BitLength,
                        signal.ByteOrder,
                        signal.IsSigned,
                        signal.Factor,
                        signal.Offset,
                        signal.Minimum,
                        signal.Maximum,
                        signal.Unit,
                        signal.Receivers,
                        _valueDescriptions.TryGetValue(signal.Name, out List<DbcValueDescription>? descriptions)
                            ? descriptions
                            : [])));
            }
        }
    }
}
