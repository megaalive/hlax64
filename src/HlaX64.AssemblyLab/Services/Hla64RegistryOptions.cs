using System.Collections.Generic;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace HlaX64.AssemblyLab.Services;

/// <summary>
/// TextMate registry with bundled HlaX64 grammar (source.hla64) on top of DarkPlus defaults.
/// </summary>
public sealed class Hla64RegistryOptions : IRegistryOptions
{
    private readonly RegistryOptions _defaults = new(ThemeName.DarkPlus);
    private readonly IRawGrammar? _hla64Grammar;

    public Hla64RegistryOptions()
    {
        var grammarPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "syntax",
            "hla64",
            "syntaxes",
            "hla64.tmLanguage.json");

        if (File.Exists(grammarPath))
        {
            using var reader = new StreamReader(grammarPath);
            _hla64Grammar = GrammarReader.ReadGrammarSync(reader);
        }
    }

    public ICollection<string> GetInjections(string scopeName) => _defaults.GetInjections(scopeName);

    public IRawTheme GetTheme(string scopeName) => _defaults.GetTheme(scopeName);

    public IRawTheme GetDefaultTheme() => _defaults.GetDefaultTheme();

    public IRawGrammar GetGrammar(string scopeName)
    {
        if (scopeName == SourceEditorSetup.GrammarScope && _hla64Grammar != null)
            return _hla64Grammar;

        return _defaults.GetGrammar(scopeName);
    }

    public ICollection<Language> GetAvailableLanguages() => _defaults.GetAvailableLanguages();

    public Language GetLanguageByExtension(string extension)
    {
        if (extension.Equals(".hla64", StringComparison.OrdinalIgnoreCase))
        {
            return new Language
            {
                Id = "hla64",
                Aliases = new List<string> { "HlaX64", "hla64" },
                Extensions = new List<string> { ".hla64" }
            };
        }

        return _defaults.GetLanguageByExtension(extension);
    }

    public string GetScopeByExtension(string extension)
    {
        if (extension.Equals(".hla64", StringComparison.OrdinalIgnoreCase))
            return SourceEditorSetup.GrammarScope;

        return _defaults.GetScopeByExtension(extension);
    }

    public string GetScopeByLanguageId(string languageId)
    {
        if (languageId.Equals("hla64", StringComparison.OrdinalIgnoreCase))
            return SourceEditorSetup.GrammarScope;

        return _defaults.GetScopeByLanguageId(languageId);
    }

    public ICollection<GrammarDefinition> GetAvailableGrammarDefinitions()
        => _defaults.GetAvailableGrammarDefinitions().ToList();
}
