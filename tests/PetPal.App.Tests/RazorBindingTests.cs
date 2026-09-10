using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using PetPal.App.Ui.Services;

namespace PetPal.App.Tests;

/// <summary>
/// Razor-un incə davranışına qarşı qoruyucu:
///
///   &lt;LoadingState Error="_error" /&gt;      → "_error" HƏRFİ MƏTNİ ötürülür
///   &lt;LoadingState Error="@_error" /&gt;     → _error sahəsinin dəyəri ötürülür
///
/// Səbəb: parametr <c>string</c> tipindədirsə, Razor dırnaq içindəki dəyəri
/// mətn sabiti sayır. <c>bool</c>/<c>int</c> kimi tiplərdə isə C# ifadəsi kimi
/// oxuyur — ona görə <c>IsLoading="_loading"</c> işləyir, <c>Error="_error"</c> isə yox.
///
/// Bu səhv kompilyasiya xətası vermir və yalnız işləyən app-də görünür:
/// bizim halda ana ekran daim "_error" yazan xəta ekranı göstərirdi.
/// </summary>
public class RazorBindingTests
{
    /// <summary>Sahə adı (_x), nöqtəli yol (a.B.c) və ya metod çağırışı — hamısı ifadədir.</summary>
    private static readonly Regex LooksLikeExpression = new(
        @"^(_[A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+.*|[A-Za-z_][A-Za-z0-9_]*\(.*\))$",
        RegexOptions.Compiled);

    [Fact]
    public void StringParametrler_IfadeOtururkenAtPrefiksiIleYazilmalidir()
    {
        var stringParameters = FindStringParameterNames();
        Assert.NotEmpty(stringParameters);

        var problems = new List<string>();

        foreach (var file in Directory.EnumerateFiles(UiProjectDirectory(), "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            foreach (var parameter in stringParameters)
            {
                // Yalnız dırnaqlı, @ ilə başlamayan dəyərlər yoxlanılır.
                foreach (Match match in Regex.Matches(source, $@"\b{parameter}=""(?<value>[^""@][^""]*)""").Cast<Match>())
                {
                    var value = match.Groups["value"].Value;

                    if (LooksLikeExpression.IsMatch(value))
                        problems.Add($"{Path.GetFileName(file)}: {parameter}=\"{value}\" → {parameter}=\"@{value}\" olmalıdır");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "String parametrlərə C# ifadəsi @ olmadan ötürülüb:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    /// <summary>UI kitabxanasındakı bütün komponentlərin <c>string</c> tipli parametrləri.</summary>
    private static List<string> FindStringParameterNames() =>
        typeof(AppState).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property =>
                property.GetCustomAttribute<ParameterAttribute>() is not null &&
                property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .Distinct()
            .ToList();

    /// <summary>Test faylının yerindən repozitoriya kökünü tapır — build çıxışından asılı deyil.</summary>
    private static string UiProjectDirectory([CallerFilePath] string testFilePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testFilePath)!;
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));

        return Path.Combine(repositoryRoot, "src", "PetPal.App.Ui");
    }
}
