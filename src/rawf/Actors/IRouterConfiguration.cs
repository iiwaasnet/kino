using System.Collections.Generic;

namespace rawf.Actors
{
    public interface IRouterConfiguration
    {
        string GetRouterAddress();
        string GetLocalScaleOutAddress();
        IEnumerable<string> GetScaleOutCluster();
    }
}