#nullable enable

using System;
using System.IO;

namespace Lan.ImageViewer.Prism
{
    /// <summary>
    /// Resolves the writable layer-configuration file the application loads and saves.
    /// <para>
    /// The configuration shipped with a host application lives in its build output
    /// (<c>CopyToOutputDirectory</c>), so a build silently restores the shipped layer
    /// definitions over whatever the running application saved — the edit appears to be
    /// lost the next time the application starts. The runtime copy therefore lives in the
    /// user's local application data and is seeded from the shipped file only when it does
    /// not exist yet.
    /// </para>
    /// </summary>
    public static class LayerConfigurationStore
    {
        /// <summary>Folder name created under the user's local application data.</summary>
        public const string ApplicationFolderName = "Lan.Shapes";

        /// <summary>File name used when the shipped path has no usable file name.</summary>
        public const string DefaultFileName = "LanShapesConfig.json";

        /// <summary>
        /// Directory holding the runtime layer configuration. Defaults to
        /// <c>%LOCALAPPDATA%\Lan.Shapes</c>.
        /// </summary>
        public static string RuntimeDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName);

        /// <summary>
        /// Returns the path of the runtime configuration file, creating it from
        /// <paramref name="shippedConfigurationPath"/> when it does not exist yet.
        /// An existing runtime file is never overwritten, so saved layer definitions survive
        /// builds, cleans and deployments.
        /// </summary>
        /// <param name="shippedConfigurationPath">Read-only default shipped with the application.</param>
        /// <param name="runtimeDirectory">Overrides <see cref="RuntimeDirectory"/> (tests).</param>
        /// <param name="fileName">Overrides the file name taken from the shipped path (tests).</param>
        /// <exception cref="ArgumentException"><paramref name="shippedConfigurationPath"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The shipped default is missing.</exception>
        public static string ResolveRuntimeFile(
            string shippedConfigurationPath,
            string? runtimeDirectory = null,
            string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(shippedConfigurationPath))
            {
                throw new ArgumentException(
                    "A shipped layer-configuration path is required.", nameof(shippedConfigurationPath));
            }

            var shipped = Path.GetFullPath(shippedConfigurationPath);
            var directory = string.IsNullOrWhiteSpace(runtimeDirectory) ? RuntimeDirectory : runtimeDirectory;
            var name = string.IsNullOrWhiteSpace(fileName)
                ? Path.GetFileName(shipped)
                : fileName;
            if (string.IsNullOrWhiteSpace(name)) name = DefaultFileName;
            var runtime = Path.GetFullPath(Path.Combine(directory, name));

            if (string.Equals(runtime, shipped, StringComparison.OrdinalIgnoreCase) || File.Exists(runtime))
            {
                return runtime;
            }

            if (!File.Exists(shipped))
            {
                throw new InvalidOperationException(
                    $"The default layer configuration '{shipped}' was not found, so the writable " +
                    $"configuration '{runtime}' cannot be created.");
            }

            Directory.CreateDirectory(directory);
            File.Copy(shipped, runtime);
            return runtime;
        }
    }
}
