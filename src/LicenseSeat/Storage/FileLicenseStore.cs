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

            // No File.Exists pre-check: it returns false for a file that exists
            // but is not accessible, which would turn "cannot read the device
            // record" into "no device record" and activate a new device. Only a
            // genuinely missing file or directory means absent; anything else
            // throws and is treated as a (possibly transient) read fault.
            // On Windows (notably .NET Framework) opening the file while a
            // concurrent File.Replace swaps it is a sharing violation; retry
            // briefly, exactly like Write and Delete, before reporting a fault.
            FileStream opened;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    opened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    break;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (DirectoryNotFoundException)
                {
                    return null;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < 5)
                {
                    System.Threading.Thread.Sleep(20 * attempt);
                }
            }

            // Share write and delete so a concurrent atomic replace (or a
            // revocation's delete) is never blocked by a reader on Windows.
            using var stream = opened;
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
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    // File.Delete is a no-op for a missing file; no File.Exists
                    // pre-check, which would silently skip an inaccessible one.
                    File.Delete(path);
                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < 5)
                {
                    System.Threading.Thread.Sleep(20 * attempt);
                }
            }
        }

        /// <inheritdoc />
        public override string ToString() => "FileLicenseStore:" + Directory;

        private string PathFor(string entryName)
        {
            if (!SecurityValidation.IsSafeIdentifier(entryName, 1, 128))
            {
                throw new ArgumentException("Entry name must be a safe identifier no longer than 128 bytes.", nameof(entryName));
            }

            return Path.Combine(Directory, entryName + FileExtension);
        }

        // netstandard2.0 has no File.Move(overwrite: true). File.Replace swaps an
        // existing destination atomically; File.Move covers a missing one. The
        // existence check is re-evaluated on every attempt, so a concurrent
        // writer creating or removing the destination is handled, and a short
        // bounded backoff rides out transient sharing violations (antivirus,
        // indexers, sync clients) that are routine on Windows.
        private static void Replace(string source, string destination)
        {
            const int Attempts = 5;
            for (var attempt = 1; ; attempt++)
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
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < Attempts)
                {
                    System.Threading.Thread.Sleep(20 * attempt);
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
