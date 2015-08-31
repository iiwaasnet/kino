using System;

namespace kino.Framework
{
    public interface IExpirableItemCollectionConfiguration
    {
        TimeSpan EvaluationInterval { get; }
    }
}