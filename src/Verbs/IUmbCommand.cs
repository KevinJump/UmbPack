using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.Packager.CI.Verbs
{
    /// <summary>
    ///  Command interface - all commands impliment this, so we can find them.
    /// </summary>
    internal interface IUmbCommand<TOptions>
        where TOptions : IUmbOptions
    {
        Task<int> Run(TOptions options);
    }

    /// <summary>
    ///  Options for a command,
    /// </summary>
    internal interface IUmbOptions
    {

    }
}
