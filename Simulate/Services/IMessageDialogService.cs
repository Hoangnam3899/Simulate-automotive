using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public interface IMessageDialogService
    {
        IReadOnlyList<DbcMessage>? SelectMessages(DbcDocument document, IReadOnlyList<DbcMessage> alreadyAddedMessages);
    }
}
