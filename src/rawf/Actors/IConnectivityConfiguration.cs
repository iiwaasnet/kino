using System.Collections.Generic;

namespace rawf.Actors
{
    public interface IConnectivityConfiguration
    {
        string GetRouterAddress();
        string GetLocalScaleOutAddress();
        IEnumerable<string> GetScaleOutCluster();
    }
}