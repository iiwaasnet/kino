using System;
using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    [AttributeUsage(AttributeTargets.Enum)]
    public class PerformanceCounterCategoryAttribute : Attribute
    {
        public PerformanceCounterCategoryAttribute(string categoryName)
            : this(categoryName, PerformanceCounterCategoryType.SingleInstance)
        {
        }

        public PerformanceCounterCategoryAttribute(string categoryName,
                                                   PerformanceCounterCategoryType categoryType)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName));
            }

            CategoryName = categoryName;
            CategoryType = categoryType;
        }

        public string CategoryName { get; }

        public PerformanceCounterCategoryType CategoryType { get; set; }
    }
}