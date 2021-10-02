using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using fatcat.Core;
using fatcat.Display;
using Microsoft.Extensions.DependencyInjection;

namespace fatcat
{
    public static class Program
    {
        public static Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<FatSystem>()
                    .BuildServiceProvider();
            var result = Parser.Default.ParseArguments(args, typeof(ListPath));
            var tasks = new List<Task>();
            tasks.Add(result.WithParsedAsync<ListPath>(async o =>
            {
                using (var stream = File.OpenRead(o.ImagePath))
                {
                    var system = serviceProvider.GetRequiredService<FatSystem>();
                    await system.Initialize(stream, o.GlobalOffset).ConfigureAwait(false);
                    await system.List(new FatPath(o.Path), o.ListDeleted);
                }
            }));

            return Task.WhenAll(tasks);
        }
    }
}
