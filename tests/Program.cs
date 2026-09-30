using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(Path.Combine(args[0], "tModLoader.dll")))
        {
            Console.Error.WriteLine("Usage: dotnet ThaiRuntimeTests.dll <tModLoader installation>");
            return 2;
        }

        var game = Path.GetFullPath(args[0]);
        var paths = Directory.EnumerateFiles(Path.Combine(game, "Libraries"), "*.dll", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "runtime", StringComparison.OrdinalIgnoreCase)
                && !p.Contains(Path.DirectorySeparatorChar + "Native" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !p.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Path.GetFileNameWithoutExtension)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);
        paths["tModLoader"] = Path.Combine(game, "tModLoader.dll");
        AssemblyLoadContext.Default.Resolving += (context, name) => paths.TryGetValue(name.Name!, out var path)
            ? context.LoadFromAssemblyPath(path) : null;
        return Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run() => Tests.Run();
}
