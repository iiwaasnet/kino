#if NET47
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace kino.Core.Diagnostics.Performance
{
    [ExcludeFromCodeCoverage]
    [RunInstaller(true)]
    public sealed class PerformanceCounterInstaller : Installer
    {
        private string binPath;
        private static readonly string CategoryAttributeFullName = typeof(PerformanceCounterCategoryAttribute).FullName;
        private static readonly string CounterDefinitionAttributeFullName = typeof(PerformanceCounterDefinitionAttribute).FullName;

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            AddPerformanceCounterInstallers();

            base.OnBeforeInstall(savedState);
        }

        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            AddPerformanceCounterInstallers();

            base.OnBeforeUninstall(savedState);
        }

        private void AddPerformanceCounterInstallers()
        {
            try
            {
                binPath = Path.GetDirectoryName(GetType().Assembly.Location);
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveAssembly;

                DiscoverPerformanceCounters();
            }
            catch (ReflectionTypeLoadException tex)
            {
                Context.LogMessage($"ERROR - {tex}" +
                                   $"{Environment.NewLine}" +
                                   $"{string.Join<Exception>(Environment.NewLine, tex.LoaderExceptions)}");
                throw;
            }
            catch (Exception ex)
            {
                Context.LogMessage($"ERROR - {ex}");
                throw;
            }
            finally
            {
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= ResolveAssembly;
            }
        }

        private Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            var assemblyPath = Path.Combine(binPath, $"{assemblyName.Name}.dll");

            Context.LogMessage($"Resolving - {args.Name} referenced by {args.RequestingAssembly} in {assemblyPath}");

            if (File.Exists(assemblyPath))
            {
                return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
            }

            var matching = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies()
                                    .Where(a => a.GetName().Name.Equals(assemblyName.Name))
                                    .ToArray();

            return matching.Length > 0
                       ? matching[0]
                       : Assembly.ReflectionOnlyLoad(assemblyName.FullName);
        }

        private void DiscoverPerformanceCounters()
        {
            var candidateAssemblies = GetCandidateAssemblies().ToList();
            foreach (var assembly in candidateAssemblies)
            {
                Context.LogMessage($"Scanning - {assembly.GetName()} for Performance Counters...");

                foreach (var categoryData in FindPerformanceCounters(assembly))
                {
                    var enumType = categoryData.EnumType;
                    var category = categoryData.Category;

                    Context.LogMessage($"Found - Performance Counters Category {enumType}");

                    var categoryName = category.CategoryName;

                    var installer = new Lazy<System.Diagnostics.PerformanceCounterInstaller>(() =>
                                                                                                 new System.Diagnostics.PerformanceCounterInstaller
                                                                                                 {
                                                                                                     CategoryName = categoryName,
                                                                                                     CategoryHelp = categoryName,
                                                                                                     CategoryType = category.CategoryType
                                                                                                 });

                    foreach (var counter in GetCounterDefinitions(enumType))
                    {
                        var counterDescription = !string.IsNullOrWhiteSpace(counter.Description)
                                                     ? counter.Description
                                                     : $"{categoryName}/{counter.Name}";

                        Context.LogMessage($"Processing - ({counter.Type}){categoryName}/{counter.Name}");

                        installer.Value.Counters.Add(new CounterCreationData(counter.Name, counterDescription, counter.Type));
                    }

                    if (installer.IsValueCreated)
                    {
                        Context.LogMessage($"Adding - installer for Performance Counters Category {enumType}");
                        Installers.Add(installer.Value);
                    }
                }
            }
        }

        private static IEnumerable<PerfCounterType> FindPerformanceCounters(Assembly assembly)
        {
            return
                assembly.GetExportedTypes()
                        .Where(type => type.IsEnum)
                        .SelectMany(CustomAttributeData.GetCustomAttributes, (type, attr) => new {Type = type, Attribute = attr})
                        .Where(attr => attr.Attribute.Constructor.DeclaringType.FullName == CategoryAttributeFullName)
                        .Select(attr => new PerfCounterType {EnumType = attr.Type, Category = GetCategoryData(attr.Attribute)});
        }

        private static IEnumerable<PerformanceCounterDefinitionAttribute> GetCounterDefinitions(Type type)
        {
            return
                Enum.GetNames(type)
                    .SelectMany(type.GetMember, (_, enumMember) => new {EnumMemberInfo = enumMember})
                    .SelectMany(enumMember => CustomAttributeData.GetCustomAttributes(enumMember.EnumMemberInfo), (_, attributeData) => new {AttributeData = attributeData})
                    .Where(attr => attr.AttributeData.Constructor.DeclaringType.FullName == CounterDefinitionAttributeFullName)
                    .Select(attr => GetCounterDefinitionData(attr.AttributeData));
        }

        private static PerformanceCounterCategoryAttribute GetCategoryData(CustomAttributeData attr)
        {
            var categoryAttribute = new PerformanceCounterCategoryAttribute(attr.ConstructorArguments.First(ca => ca.ArgumentType.FullName == typeof(string).FullName).Value.ToString());
            if (attr.ConstructorArguments.Count == 2)
            {
                categoryAttribute.CategoryType = (PerformanceCounterCategoryType) attr.ConstructorArguments
                                                                                      .First(ca => ca.ArgumentType.FullName == typeof(PerformanceCounterCategoryType).FullName)
                                                                                      .Value;
            }

            foreach (var namedArgument in attr.NamedArguments)
            {
                var propertyInfo = categoryAttribute.GetType().GetProperty(namedArgument.MemberName);
                propertyInfo?.SetValue(categoryAttribute, namedArgument.TypedValue.Value);
            }

            return categoryAttribute;
        }

        private static PerformanceCounterDefinitionAttribute GetCounterDefinitionData(CustomAttributeData definition)
        {
            var data = new PerformanceCounterDefinitionAttribute((string) definition.ConstructorArguments[0].Value,
                                                                 (PerformanceCounterType) definition.ConstructorArguments[1].Value);

            foreach (var namedArgument in definition.NamedArguments)
            {
                var propertyInfo = data.GetType().GetProperty(namedArgument.MemberName);
                propertyInfo?.SetValue(data, namedArgument.TypedValue.Value);
            }

            return data;
        }

        private IEnumerable<Assembly> GetCandidateAssemblies()
        {
            var perfCounterAsmName = GetType().Assembly.GetName().Name;
            return Directory.EnumerateFiles(binPath, "*.dll")
                            .Concat(Directory.EnumerateFiles(binPath, "*.exe"))
                            .Select(Assembly.ReflectionOnlyLoadFrom)
                            .Where(asm => ShouldScan(asm, perfCounterAsmName)
                                          || asm.GetName().Name == perfCounterAsmName);
        }

        private static bool ShouldScan(Assembly assembly, string perfCounterAsmName)
            => assembly.GetReferencedAssemblies().Any(name => name.Name == perfCounterAsmName);

        private class PerfCounterType
        {
            public Type EnumType { get; set; }

            public PerformanceCounterCategoryAttribute Category { get; set; }
        }
    }
}
#endif