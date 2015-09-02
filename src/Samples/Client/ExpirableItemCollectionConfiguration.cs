using System;
using kino.Framework;

namespace Client
{
    public class ExpirableItemCollectionConfiguration : IExpirableItemCollectionConfiguration
    {
        public TimeSpan EvaluationInterval { get; set;  }
    }
}