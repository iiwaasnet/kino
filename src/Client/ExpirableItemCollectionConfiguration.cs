using System;
using rawf.Framework;

namespace Client
{
    public class ExpirableItemCollectionConfiguration : IExpirableItemCollectionConfiguration
    {
        public TimeSpan EvaluationInterval { get; set;  }
    }
}