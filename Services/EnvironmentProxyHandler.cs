namespace ProxySwitcher.Services;

/// <summary>
/// Manages environment variables for proxy settings.
/// Sets HTTP_PROXY, HTTPS_PROXY, and ALL_PROXY variables for command-line tools and applications.
/// </summary>
public class EnvironmentProxyHandler
{
    private const string HttpProxyVar = "HTTP_PROXY";
    private const string HttpsProxyVar = "HTTPS_PROXY";
    private const string AllProxyVar = "ALL_PROXY";

    /// <summary>
    /// Sets environment variables for proxy configuration.
    /// Sets at the User scope in the registry for persistence across sessions.
    /// </summary>
    public void SetEnvironmentVariables(string host, int port)
    {
        try
        {
            var proxyValue = $"http://{host}:{port}";

            // Set environment variables for the current process (immediate effect)
            Environment.SetEnvironmentVariable(HttpProxyVar, proxyValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(HttpsProxyVar, proxyValue, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(AllProxyVar, proxyValue, EnvironmentVariableTarget.Process);

            // Also set at User scope for persistence in new processes
            Environment.SetEnvironmentVariable(HttpProxyVar, proxyValue, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(HttpsProxyVar, proxyValue, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(AllProxyVar, proxyValue, EnvironmentVariableTarget.User);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set environment variables: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clears proxy-related environment variables.
    /// </summary>
    public void ClearEnvironmentVariables()
    {
        try
        {
            // Clear from the current process
            Environment.SetEnvironmentVariable(HttpProxyVar, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(HttpsProxyVar, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(AllProxyVar, null, EnvironmentVariableTarget.Process);

            // Clear from User scope
            Environment.SetEnvironmentVariable(HttpProxyVar, null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(HttpsProxyVar, null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(AllProxyVar, null, EnvironmentVariableTarget.User);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to clear environment variables: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current proxy environment variables.
    /// </summary>
    public (string? HttpProxy, string? HttpsProxy, string? AllProxy) GetCurrentEnvironmentVariables()
    {
        var httpProxy = Environment.GetEnvironmentVariable(HttpProxyVar);
        var httpsProxy = Environment.GetEnvironmentVariable(HttpsProxyVar);
        var allProxy = Environment.GetEnvironmentVariable(AllProxyVar);

        return (httpProxy, httpsProxy, allProxy);
    }
}
