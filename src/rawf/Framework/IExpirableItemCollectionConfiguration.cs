using System;

namespace rawf.Framework
{
    public interface IExpirableItemCollectionConfiguration
    {
        TimeSpan EvaluationInterval { get; }
    }
}