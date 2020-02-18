using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Umbraco.Packager.CI.Properties;
using Umbraco.Packager.CI.Verbs;

namespace Umbraco.Packager.CI
{
    // Exit code conventions
    // https://docs.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN

    public class Program
    {
        static TextReader reader;
        static TextWriter writer;

        static ServiceProvider serviceProvider;

        public static async Task Main(string[] args)
        {
            reader = Console.In;
            writer = Console.Out;

            serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<IUmbCommand<PackOptions>, PackCommand>()
                .AddSingleton<IUmbCommand<InitOptions>, InitCommand>()
                .AddSingleton<IUmbCommand<PushOptions>, PushCommand>()
                .BuildServiceProvider();

            var logger = serviceProvider
                .GetService<ILoggerFactory>()
                .CreateLogger<Program>();


            // now uses 'verbs' so each verb is a command
            // 
            // e.g umbpack init or umbpack push
            // these are handled by the Command classes.
            var parser = new CommandLine.Parser(with => {
                with.HelpWriter = null;
                with.AutoVersion = false;
                with.CaseSensitive = false;
            } );

            var parserResults = parser.ParseArguments<PackOptions, PushOptions, InitOptions>(args);

            parserResults
                .WithParsed<PackOptions>(async opts => await RunCommand(opts))
                .WithParsed<PushOptions>(async opts => await RunCommand(opts))
                .WithParsed<InitOptions>(async opts => await RunCommand(opts))
                .WithNotParsed(async errs => await DisplayHelp(parserResults, errs));

            Environment.Exit(0);
        }

        /// <summary>
        ///  Runs the required command, based on the supplied options.
        /// </summary>
        static async Task<int> RunCommand<TOptions>(TOptions options)
            where TOptions : IUmbOptions
        {
            var command = serviceProvider.GetService<IUmbCommand<TOptions>>();
            return await command.Run(options);
        }

        static async Task DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AutoVersion = false;
                return h;
            }, e => e);
            
            // Append header with Ascaii Art
            helpText.Heading = Resources.Ascaii + Environment.NewLine + helpText.Heading;
            await writer.WriteLineAsync(helpText);

            // --version or --help
            if (errs.IsVersion() || errs.IsHelp())
            {
                // 0 is everything is all OK exit code
                Environment.Exit(0);
            }

            // ERROR_INVALID_FUNCTION
            Environment.Exit(1);
        }

    }

}
