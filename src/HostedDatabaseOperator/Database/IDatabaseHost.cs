using System;
using System.Threading.Tasks;

namespace HostedDatabaseOperator.Database
{
    public interface IDatabaseHost : IAsyncDisposable
    {
        Task<bool> CanConnect();
        
    }
}
