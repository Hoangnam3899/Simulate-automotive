using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate
{
    /// <summary>
    /// Creates application defaults at the composition boundary without leaking Vector dependencies into view models.
    /// </summary>
    internal static class ApplicationComposition
    {
        /// <summary>
        /// Creates the connection view model backed by the production Vector adapter.
        /// </summary>
        /// <returns>A connection view model ready for the existing UI binding path.</returns>
        public static ConnectionViewModel CreateConnectionViewModel()
        {
            return new ConnectionViewModel(new VectorHardwareService());
        }
    }
}
