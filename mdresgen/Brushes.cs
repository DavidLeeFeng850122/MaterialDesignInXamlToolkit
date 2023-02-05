using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace mdresgen;

public static class Brushes
{
    private record struct CThingy(Color Color, double Alpha);

    public static async Task DoStuffAsync()
    {
        using var inputFile = File.OpenRead("ThemeBrushes.json");
        Brush[] brushes = await JsonSerializer.DeserializeAsync<Brush[]>(inputFile)
            ?? throw new InvalidOperationException("Did not find brushes from source file");

        var lightBrushes = brushes.Select(x => x.ThemeValues["Light"])
            .Distinct()
            .Select(ColorConverter.ConvertFromString)
            .OfType<Color>()
            .Where(IsGray)
            .OrderByDescending(AlphaPercentage)
            .ThenByDescending(GrayValue)
            .ToArray();

        var blackBrushes = brushes.Select(x => x.ThemeValues["Dark"])
            .Distinct()
            .Select(ColorConverter.ConvertFromString)
            .OfType<Color>()
            .Where(IsGray)
            .OrderByDescending(AlphaPercentage)
            .ThenByDescending(GrayValue)
            .ToArray();

        static bool IsGray(Color color) => color.R == color.G && color.R == color.B;
        static int GrayValue(Color color) => (int)(color.R / 255.0 * 100);
        //0 = black, 100 = white
        static int AlphaPercentage(Color color)
            => (int)(color.A / 255.0 * 100);

        //Generate tonal palette
        foreach (CThingy c in from brush in brushes
                            let lightBrushString = brush.ThemeValues["Light"]
                            let color = (Color)ColorConverter.ConvertFromString(lightBrushString)
                            where IsGray(color)
                            let alpha = AlphaPercentage(color)
                            orderby alpha descending
                            select new CThingy(color, alpha))
        {
            
        }

        //var darkBrushes = brushes.GroupBy(x => x.ThemeValues["Dark"]).ToArray();

        brushes = brushes.OrderBy(x => x.Name).ToArray();

        TreeItem<Brush> brushTree = BuildBrushTree(brushes);

        DirectoryInfo repoRoot = GetRepoRoot() ?? throw new InvalidOperationException("Failed to find the repo root");

        GenerateBuiltinThemingDictionaries(brushes, repoRoot);
    }

    private static void GenerateBuiltinThemingDictionaries(IEnumerable<Brush> brushes, DirectoryInfo repoRoot)
    {
        WriteFile("Light");
        WriteFile("Dark");

        void WriteFile(string theme)
        {
            using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "Themes", $"MaterialDesignTheme.{theme}.xaml"));
            writer.WriteLine($"""
                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options">
                """);
            foreach (Brush brush in brushes)
            {
                writer.WriteLine($$"""
                      <Color x:Key="{{brush.Name}}.Color">{{brush.ThemeValues[theme]}}</Color>
                      <SolidColorBrush x:Key="{{brush.Name}}" Color="{StaticResource {{brush.Name}}.Color}" po:Freeze="True" />
                    """);
            }

            writer.WriteLine("</ResourceDictionary>");
        }
    }

    private static DirectoryInfo? GetRepoRoot()
    {
        DirectoryInfo? currentDirectory = new(Environment.CurrentDirectory);
        while (currentDirectory is not null && !currentDirectory.EnumerateDirectories(".git").Any())
        {
            currentDirectory = currentDirectory.Parent;
        }
        return currentDirectory;
    }

    private static TreeItem<Brush> BuildBrushTree(IReadOnlyList<Brush> brushes)
    {
        TreeItem<Brush> root = new("");

        foreach (Brush brush in brushes)
        {
            TreeItem<Brush> current = root;
            foreach (string part in brush.ContainerParts)
            {
                TreeItem<Brush>? child = current.Children.FirstOrDefault(x => x.Name == part);
                if (child is null)
                {
                    child = new(part);
                    current.Children.Add(child);
                }
                current = child;
            }
            current.Values.Add(brush);
        }

        return root;
    }
}

[DebuggerDisplay($"{{{nameof(Name)}}} [Values: {{{nameof(Values)}.Count}}] [Children: {{{nameof(Children)}.Count}}]")]
public class TreeItem<T> : IEnumerable<T>
{
    public string Name { get; }

    public TreeItem(string name) => Name = name;

    public List<T> Values { get; } = new();

    public List<TreeItem<T>> Children { get; } = new();

    public IEnumerator<T> GetEnumerator()
    {
        foreach (T value in Values)
        {
            yield return value;
        }
        foreach (TreeItem<T> child in Children)
        {
            foreach (T value in child)
            {
                yield return value;
            }
        }
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public record class Brush(
    [property:JsonPropertyName("name")]
    string? Name,
    [property:JsonPropertyName("themeValues")]
    ThemeValues ThemeValues,
    [property:JsonPropertyName("alternateKeys")]
    string[]? AlternateKeys)
{
    public const string BrushPrefix = "MaterialDesign.Brush.";
    public string PropertyName => Name!.Split(".")[^1];
    public string NameWithoutPrefix => Name![BrushPrefix.Length..];
    public string[] ContainerParts => Name!.Split('.')[2..^1];
    public string ContainerTypeName => string.Join('.', ContainerParts);
}


public record class ThemeValues(
    [property:JsonPropertyName("light")]
    string Light,
    [property:JsonPropertyName("dark")]
    string Dark)
{
    public string this[string theme]
    {
        get
        {
            return theme.ToLowerInvariant() switch
            {
                "light" => Light,
                "dark" => Dark,
                _ => throw new InvalidOperationException($"Unknown theme: {theme}")
            };
        }
    }
}

