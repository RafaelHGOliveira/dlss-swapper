using Nito.AsyncEx;
using SQLite;

namespace DLSS_Swapper.Core.Interfaces;

public interface IDatabase
{
    AsyncLock Mutex { get; }
    SQLiteAsyncConnection Connection { get; }
}
