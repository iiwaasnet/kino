using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace kino.Core.Diagnostics.Performance
{
    public class PerformanceCounterManager<TCategory> : IPerformanceCounterManager<TCategory> where TCategory : struct
    {
        private readonly Dictionary<TCategory, IPerformanceCounter> counters;
        private readonly string instanceName;

        public PerformanceCounterManager(IInstanceNameResolver instanceNameResolver, ILogger logger)
        {
            instanceName = instanceNameResolver.GetInstanceName();
            counters = new Dictionary<TCategory, IPerformanceCounter>();

            CreatePerformanceCounters(logger);
        }

        private void CreatePerformanceCounters(ILogger logger)
        {
            var categoryType = typeof(TCategory);
            var thisTypeName = GetType().Name;

            var category = GetPerformanceCountersCategory(categoryType, thisTypeName);

            foreach (var enumValue in Enum.GetValues(categoryType).Cast<TCategory>())
            {
                var counterDefinition = GetPerformanceCounterDefinition(categoryType, enumValue);

                counters.Add(enumValue, new SafePerformanceCounter(category.CategoryName, counterDefinition.Name, instanceName, logger));
            }
        }

        private static PerformanceCounterDefinitionAttribute GetPerformanceCounterDefinition(Type categoryType, TCategory enumValue)
        {
            var counterAttrTypeName = typeof(PerformanceCounterDefinitionAttribute).Name;
            var counterDefinition = TryGetAttribute<PerformanceCounterDefinitionAttribute>(categoryType.GetField(enumValue.ToString()));

            if (counterDefinition == null)
            {
                throw new ArgumentException($"The enum values of {categoryType.FullName} must be decorated with {counterAttrTypeName} attribute");
            }
            return counterDefinition;
        }

        private static PerformanceCounterCategoryAttribute GetPerformanceCountersCategory(Type categoryType, string thisTypeName)
        {
            var categoryAttrTypeName = typeof(PerformanceCounterCategoryAttribute).Name;
            if (!categoryType.IsEnum)
            {
                throw new ArgumentException($"The type argument of {thisTypeName} must be an enum!");
            }

            var category = TryGetAttribute<PerformanceCounterCategoryAttribute>(typeof(TCategory));
            if (category == null)
            {
                throw new ArgumentException($"The type argument of {thisTypeName} must be decorated with the {categoryAttrTypeName} attribute!");
            }

            return category;
        }

        private static T TryGetAttribute<T>(MemberInfo type) where T : class
            => Attribute.GetCustomAttribute(type, typeof(T), false) as T;

        public IPerformanceCounter GetCounter(TCategory counter)
        {
            IPerformanceCounter performanceCounter;
            if (counters.TryGetValue(counter, out performanceCounter))
            {
                return performanceCounter;
            }

            throw new KeyNotFoundException($"Performance counter {counter} does not exist!");
        }
    }
}