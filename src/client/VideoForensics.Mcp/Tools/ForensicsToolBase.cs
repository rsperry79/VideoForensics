using Microsoft.Extensions.Logging;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>Abstract base class for forensics tool implementations. Consolidates common logging and repository patterns.</summary>
    public abstract class ForensicsToolBase
    {
        /// <summary>Logger instance for tool method invocations and diagnostics.</summary>
        protected ILogger Logger { get; }

        /// <summary>Initialize base tool with logger dependency.</summary>
        /// <param name="logger">Logger for this tool type.</param>
        protected ForensicsToolBase(ILogger logger)
        {
            Logger = logger;
        }
    }
}
