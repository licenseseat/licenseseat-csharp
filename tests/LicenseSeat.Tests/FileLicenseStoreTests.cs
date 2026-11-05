using System.Text;
using System.Text.Json;

namespace LicenseSeat.Tests;

public sealed class FileLicenseStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "licenseseat-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void RoundTrip_CreatesDirectoryOverwritesAndDeletes()
    {
        var store = new FileLicenseStore(_directory);
        Assert.Null(store.Read("licenseseat_license"));
        Assert.False(Directory.Exists(_directory));

        store.Write("licenseseat_license", "{\"a\":1}");
        Assert.Equal("{\"a\":1}", store.Read("licenseseat_license"));
        Assert.True(File.Exists(Path.Combine(_directory, "licenseseat_license.json")));

        store.Write("licenseseat_license", "{\"a\":\"Olá 👋\"}");
        Assert.Equal("{\"a\":\"Olá 👋\"}", store.Read("licenseseat_license"));

        store.Delete("licenseseat_license");
        Assert.Null(store.Read("licenseseat_license"));
        store.Delete("licenseseat_license");

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData(".hidden")]
    public void RejectsUnsafeEntryNames(string entryName)
    {
        var store = new FileLicenseStore(_directory);
        Assert.Throws<ArgumentException>(() => store.Read(entryName));
        Assert.Throws<ArgumentException>(() => store.Write(entryName, "{}"));
        Assert.Throws<ArgumentException>(() => store.Delete(entryName));
    }

    [Fact]
    public void RejectsRelativeOrEmptyDirectory()
    {
        Assert.Throws<ArgumentException>(() => new FileLicenseStore("relative/path"));
        Assert.Throws<ArgumentException>(() => new FileLicenseStore(" "));
        Assert.Throws<ArgumentException>(() => new FileLicenseStore(null!));
    }

    [Fact]
    public void RejectsOversizedAndInvalidUtf8Entries()
    {
        var store = new FileLicenseStore(_directory);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "big.json"), new byte[FileLicenseStore.MaxEntryBytes + 1]);
        File.WriteAllBytes(Path.Combine(_directory, "bad.json"), new byte[] { 0x7b, 0xff, 0x7d });

        Assert.Throws<InvalidDataException>(() => store.Read("big"));
        Assert.Throws<DecoderFallbackException>(() => store.Read("bad"));
        Assert.Throws<ArgumentException>(() => store.Write("big", new string('x', FileLicenseStore.MaxEntryBytes + 1)));
        Assert.Throws<ArgumentNullException>(() => store.Write("big", null!));
    }

    [Fact]
    public async Task ConcurrentWriters_LeaveOneCompleteValue()
    {
        var store = new FileLicenseStore(_directory);
        var values = Enumerable.Range(0, 40).Select(i => JsonSerializer.Serialize(new { writer = i, padding = new string('p', 4096) })).ToArray();

        await Task.WhenAll(values.Select(value => Task.Run(() =>
        {
            // Concurrent replace can lose a race on some platforms; the
            // contract is that the surviving file is always one whole value.
            try
            {
                store.Write("licenseseat_offline_token", value);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        })));

        Assert.Contains(store.Read("licenseseat_offline_token"), values);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void ForApplication_UsesLocalNonRoamingApplicationData()
    {
        var store = FileLicenseStore.ForApplication("MatchEcho");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MatchEcho",
                "LicenseSeat")),
            store.Directory);
        Assert.Throws<ArgumentException>(() => FileLicenseStore.ForApplication("../escape"));
        Assert.Throws<ArgumentException>(() => FileLicenseStore.ForApplication(""));
    }
}
