#nullable enable
using System;
using System.IO;
using System.Text;

namespace LicenseSeat
{

    /// <summary>
    /// An <see cref="ILicenseStore"/> that keeps each entry as a small JSON file in
    /// one directory, written atomically (temporary file, then replace).
    /// </summary>
    /// <remarks>
    /// Files are plain JSON. That is deliberate: the SDK re-verifies the signed
    /// offline token on every read and never restores signing keys, cached
    /// validation results or entitlements from storage, so the files carry no
    /// authority of their own. Wrap or replace this store (for example with
    /// DPAPI) if your threat model also requires confidentiality at rest.
    /// Use a per-user, per-machine directory: a roaming profile would carry a
    /// device-bound activation to machines it is not valid on.
    /// </remarks>
    public sealed class FileLicenseStore : ILicenseStore
    {
        /// <summary>The largest entry, in bytes, this store will read.</summary>
        public const int MaxEntryBytes = 1024 * 1024;

        private const string FileExtension = ".json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// Creates a store rooted at <paramref name="directory"/>. The directory
        /// is created on the first write.
        /// </summary>
        /// <param name="directory">An absolute directory path.</param>
        public FileLicenseStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory) ||
                directory.IndexOf('\0') >= 0)
            {
                throw new ArgumentException("Storage directory must be an absolute path.", nameof(directory));
            }

            Directory = Path.GetFullPath(directory);
        }

        /// <summary>Gets the directory that holds the entries.</summary>
        public string Directory { get; }

        /// <summary>
        /// Creates a store in the current user's local (non-roaming) application
        /// data folder: <c>%LOCALAPPDATA%\{applicationName}\LicenseSeat</c> on
        /// Windows, <c>~/.local/share/{applicationName}/LicenseSeat</c> on Linux
        /// and the equivalent on macOS.
        /// </summary>
        /// <param name="applicationName">
        /// A safe folder name for your application (<c>[A-Za-z0-9._-]</c>,
        /// starting with an alphanumeric character, at most 64 bytes).
        /// </param>
        /// <returns>The store.</returns>
        public static FileLicenseStore ForApplication(string applicationName)
        {
            if (!SecurityValidation.IsSafeIdentifier(applicationName, 1, 64))
            {
                throw new ArgumentException("Application name must be a safe identifier no longer than 64 bytes.", nameof(applicationName));
            }

            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException("The local application data folder is not available on this system.");
            }

            return new FileLicenseStore(Path.Combine(root, applicationName, "LicenseSeat"));
        }

        /// <inheritdoc />
        public string? Read(string entryName)
        {
            var path = PathFor(entryName);
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxEntryBytes)
            {
                throw new InvalidDataException("Stored entry exceeds the maximum size.");
            }

            var buffer = new byte[(int)stream.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = stream.Read(buffer, read, buffer.Length - read);
                if (count == 0)
                {
                    throw new InvalidDataException("Stored entry was truncated while reading.");
                }

                read += count;
            }

            return StrictUtf8.GetString(buffer);
        }

        /// <inheritdoc />
        public void Write(string entryName, string contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var path = PathFor(entryName);
            var bytes = StrictUtf8.GetBytes(contents);
            if (bytes.Length > MaxEntryBytes)
            {
                throw new ArgumentException("Entry exceeds the maximum size.", nameof(contents));
            }

            System.IO.Directory.CreateDirectory(Directory);
            var temporaryPath = Path.Combine(Directory, $"{entryName}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                Replace(temporaryPath, path);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        /// <inheritdoc />
        public void Delete(string entryName)
        {
            var path = PathFor(entryName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string PathFor(string entryName)
        {
            if (!SecurityValidation.IsSafeIdentifier(entryName, 1, 128))
            {
                throw new ArgumentException("Entry name must be a safe identifier no longer than 128 bytes.", nameof(entryName));
            }

            return Path.Combine(Directory, entryName + FileExtension);
        }

        // netstandard2.0 has no File.Move(overwrite: true). File.Replace swaps an
        // existing destination atomically; File.Move covers a missing one. If a
        // concurrent writer creates or removes the destination between the
        // check and the call, retry once with the other primitive.
        private static void Replace(string source, string destination)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(destination))
                    {
                        File.Replace(source, destination, null);
                    }
                    else
                    {
                        File.Move(source, destination);
                    }

                    return;
                }
                catch (IOException) when (attempt == 0)
                {
                    // Retry once; see the method comment.
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // A leftover temporary file is harmless; it is never read.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above.
            }
        }
    }
}
