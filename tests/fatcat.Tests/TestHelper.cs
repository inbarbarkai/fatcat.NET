using System.IO;
using fatcat.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace fatcat.Tests
{
    internal static class TestHelper
    {
        internal static IServiceCollection CreateDefault()
        {
            var services = new ServiceCollection();

            services.AddLogging(l =>
            {
                l.ClearProviders();
                l.AddXUnit();
            });
            services.AddSingleton<FatSystem>();

            return services;
        }

        internal static Stream GetTestFileStream(string fileName)
            => File.Open(Path.Combine("TestData", fileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }
}
