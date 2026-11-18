using NextUpKids.Common.Database;

namespace NextUpKids.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
	public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nextupkids-{Guid.NewGuid():N}.db3");
	public AppDatabase Database { get; }
	public string StoragePath => Path + ".data";

	public TestDatabase() => Database = new AppDatabase(Path);

	public async ValueTask DisposeAsync()
	{
		await Database.DisposeAsync();
		foreach (var suffix in new[] { "", "-wal", "-shm" })
			File.Delete(Path + suffix);
		if (Directory.Exists(StoragePath)) Directory.Delete(StoragePath, true);
	}
}
