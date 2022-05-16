using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlazingRoute;

[Generator]
public class RouteGenerator : ISourceGenerator
{
    record RouteParameter(string ParameterType, string ParameterName);

    record PageRoutes(string PageName, ImmutableArray<string> RouteStrings);

    public class GenerationOptions
    {
        public string ClassName { get; set; } = default!;
        public string Namespace { get; set; } = default!;
        public bool ExtensionMethods { get; set; } = false;

        public GenerationOptions MakeDefault(GeneratorExecutionContext context)
        {
            ClassName ??= "Routes";
            Namespace ??= context.Compilation.AssemblyName ?? throw new InvalidOperationException("Compilation has no AssemblyName");

            return this;
        }
    }

    private const string RouteAttributeName = "Microsoft.AspNetCore.Components.RouteAttribute";

    private static readonly Regex ParametersRegex = new(@"\{(.*?)\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public void Initialize(GeneratorInitializationContext context)
    {
        //#if DEBUG
        //        if (!System.Diagnostics.Debugger.IsAttached)
        //        {
        //            System.Diagnostics.Debugger.Launch();
        //        }
        //#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var options = LoadOptions(context);

        var routes = GetRoutes(context);

        var source = GenerateClass(routes, options);

        context.AddSource(options.ClassName, source);
    }

    private static GenerationOptions LoadOptions(GeneratorExecutionContext context)
    {
        static GenerationOptions MakeDefault(GeneratorExecutionContext context, GenerationOptions? options = default!) =>
            options?.MakeDefault(context) ?? new GenerationOptions().MakeDefault(context);

        var optionsFile = context.AdditionalFiles.SingleOrDefault(f => Path.GetFileName(f.Path) == "RouteGeneration.xml");

        if (optionsFile is null || !File.Exists(optionsFile.Path)) return MakeDefault(context);

        var content = File.ReadAllText(optionsFile.Path);

        if (string.IsNullOrWhiteSpace(content)) return MakeDefault(context);

        try
        {
            var serializer = new XmlSerializer(typeof(GenerationOptions));

            using var stream = new FileStream(optionsFile.Path, FileMode.Open, FileAccess.Read);

            var options = serializer.Deserialize(stream) as GenerationOptions ?? new();

            return MakeDefault(context, options);
        }
        catch
        {
            return MakeDefault(context);
        }
    }

    private static ImmutableArray<PageRoutes> GetRoutes(GeneratorExecutionContext context)
    {
        // When blazor is using source generators,
        // then the classes generated from .razor files are not available.
        // We need to handle both classes and razor files

        var routesFromClasses = context.Compilation.SyntaxTrees
            .SelectMany(s => s.GetRoot().DescendantNodes())
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.AttributeLists
                .SelectMany(x => x.Attributes)
                .Any(attr => attr.Name.ToString() == RouteAttributeName))
            .Select(component => new PageRoutes
            (
                component.Identifier.ToString(),
                GetRoutes(context.Compilation, component)
            ))
            .ToImmutableArray();

        var routesFromRazorFiles = context.AdditionalFiles
            .Where(f => Path.GetExtension(f.Path) == ".razor")
            .Select(f =>
            {
                var routes = File.ReadAllLines(f.Path)
                    .Where(l => l.StartsWith("@page"))
                    .Select(l => l.Trim().Replace("@page ", string.Empty).Replace("\"", string.Empty))
                    .ToImmutableArray();

                return new PageRoutes
                (
                    Path.GetFileNameWithoutExtension(f.Path),
                    routes.ToImmutableArray()
                );
            })
            .Where(p => p.RouteStrings.Length != 0)
            .ToImmutableArray();

        // Combine and remove duplicates

        return Enumerable.Empty<PageRoutes>()
            .Concat(routesFromClasses)
            .Concat(routesFromRazorFiles)
            .GroupBy(p => p.PageName)
            .Select(group => new PageRoutes
            (
                group.Key,
                group.SelectMany(p => p.RouteStrings).Distinct().ToImmutableArray()
            ))
            .ToImmutableArray();
    }

    private static string GenerateClass(ImmutableArray<PageRoutes> routes, GenerationOptions options)
    {
        var builder = new StringBuilder();

        builder.Append(
@"using System.Collections.Immutable;

namespace ").Append(options.Namespace).Append(@";

public static partial class ").AppendLine(options.ClassName).Append(@"
{");

        builder.AppendLine(
@"    public static ImmutableArray<string> All { get; } = new []
    {");

        foreach (var route in routes.SelectMany(r => r.RouteStrings))
        {
            builder.Append("        \"").Append(route).AppendLine("\",");
        }

        builder.AppendLine(
"    }.ToImmutableArray();");

        builder.AppendLine();

        for (var i = 0; i < routes.Length; i++)
        {
            var route = routes[i];
            for (var j = 0; j < route.RouteStrings.Length; j++)
            {
                var path = route.RouteStrings[j];

                CreateInterpolationMethod(builder, route.PageName, path, j);
            }

            if (i != routes.Length - 1) builder.AppendLine();
        }

        builder.Append(
"}");

        return builder.ToString();
    }

    private static void CreateInterpolationMethod(StringBuilder builder, string methodName, string path, int index)
    {
        // Generate parameter data

        var parameterMatches = ParametersRegex.Matches(path);

        var parameterNames = new RouteParameter[parameterMatches.Count];

        for (var i = 0; i < parameterMatches.Count; i++)
        {
            Match parameterMatch = parameterMatches[i];

            var parts = parameterMatch.Value.TrimStart('{').TrimEnd('}').Split(':');

            RouteParameter parameter;

            // strings are only marked as: @page "/{parameter}"
            if (parts.Length == 1)
            {
                var namePart = parts[0];
                var isNullable = namePart.EndsWith("?") || namePart.StartsWith("*");

                var parameterName = namePart.TrimEnd('?').TrimStart('*');
                var parameterType = isNullable ? "string?" : "string";

                parameter = new RouteParameter(parameterType, parameterName);
            }
            else
            {
                var parameterName = parts[0];
                var parameterType = parts[1] switch
                {
                    "datetime" => "DateTime",
                    "datetime?" => "DateTime?",
                    "guid" => "Guid",
                    "guid?" => "Guid?",
                    var other => other,
                };
                parameter = new RouteParameter(parameterType, parameterName);
            }

            parameterNames[i] = parameter;
        }

        // Create route interpolation

        var interpolatedPath = path;

        for (var i = 0; i < parameterMatches.Count; i++)
        {
            var match = parameterMatches[i];
            var parameterInfo = parameterNames[i];

            var interpolationEnd = parameterInfo.ParameterType.EndsWith("?") ? "?" : string.Empty;

            if (parameterInfo.ParameterType.Contains("DateTime"))
            {
                interpolationEnd += ".ToString(\"yyyy-MM-dd HH:mm:ss\", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            else if (parameterInfo.ParameterType.Contains("Guid"))
            {
                interpolationEnd += ".ToString(\"D\", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            else
            {
                interpolationEnd += ".ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }

            interpolatedPath = interpolatedPath.Replace(match.Value, "{" + parameterInfo.ParameterName + interpolationEnd);
        }

        // Build method

        builder.Append(
@"    /// <summary>
    /// ").Append(path).AppendLine(@"
    /// </summary>");

        builder.Append("    public static string ").Append(methodName).Append(index != 0 ? index.ToString() : string.Empty).Append("(");

        foreach (var parameter in parameterNames)
        {
            builder.Append(parameter.ParameterType).Append(" ").Append(parameter.ParameterName).Append(", ");
        }

        if (parameterNames.Length > 0)
        {
            // ", "
            builder.Remove(builder.Length - 2, 2);
        }


        builder.Append(") => $\"").Append(interpolatedPath).AppendLine("\";");
    }

    private static ImmutableArray<string> GetRoutes(Compilation compilation, ClassDeclarationSyntax component)
    {
        var semanticModel = compilation.GetSemanticModel(component.SyntaxTree);

        return component.AttributeLists
            .SelectMany(x => x.Attributes)
            .Where(attribute => attribute.Name.ToString() == RouteAttributeName)
            .Select(attribute =>
            {
                var argument = attribute.ArgumentList!.Arguments[0];
                return semanticModel.GetConstantValue(argument.Expression).ToString();
            })
            .ToImmutableArray();
    }
}