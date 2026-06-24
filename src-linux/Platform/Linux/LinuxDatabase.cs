using Nito.AsyncEx;
using SQLite;
using DLSS_Swapper;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Steam;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxDatabase : IDatabase
{
    public AsyncLock Mutex { get; } = new AsyncLock();
    public SQLiteAsyncConnection Connection { get; }

    public LinuxDatabase()
    {
        Connection = new SQLiteAsyncConnection(Storage.GetDBPath());
    }

    public async Task InitializeAsync()
    {
        await Connection.CreateTableAsync<SteamGame>();
        await Connection.CreateTableAsync<GameAsset>();
        await Connection.CreateTableAsync<GameHistory>();
    }
}
