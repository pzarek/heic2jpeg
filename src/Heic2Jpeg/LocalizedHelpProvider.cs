using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Heic2Jpeg;

/// <summary>
/// Writes --help with the layout and colours of Spectre.Console.Cli's own help, but takes every text from
/// <see cref="Strings"/>. Spectre translates its headings ("USAGE", "OPTIONS", ...) only into the languages
/// it ships, and Polish is not one of them.
/// </summary>
/// <remarks>Covers what heic2jpeg needs: one default command with arguments and options, no subcommands.</remarks>
internal sealed class LocalizedHelpProvider : IHelpProvider
{
    private const string Indent = "    ";

    public IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        command ??= model.DefaultCommand;
        var parameters = command?.Parameters.Where(parameter => !parameter.IsHidden).ToList() ?? [];
        var arguments = parameters.OfType<ICommandArgument>().OrderBy(argument => argument.Position).ToList();
        var options = parameters.OfType<ICommandOption>().ToList();
        var examples = command?.Examples is { Count: > 0 } commandExamples ? commandExamples : model.Examples;

        var sections = new List<IRenderable[]>();

        if (!string.IsNullOrWhiteSpace(command?.Description))
        {
            sections.Add([Heading(Strings.HelpDescription), new Markup(Markup.Escape(Normalize(command.Description)) + "\n")]);
        }

        sections.Add([Heading(Strings.HelpUsage), new Markup(Indent + Usage(model.ApplicationName, arguments) + "\n")]);

        if (examples.Count > 0)
        {
            var lines = examples.Select(example =>
                $"{Indent}{Markup.Escape(model.ApplicationName)} [grey]{Markup.Escape(string.Join(' ', example))}[/]\n");
            sections.Add([Heading(Strings.HelpExamples), new Markup(string.Concat(lines))]);
        }

        if (arguments.Count > 0)
        {
            sections.Add([Heading(Strings.HelpArguments), Arguments(arguments)]);
        }

        sections.Add([Heading(Strings.HelpOptions), Options(options, showVersion: model.ApplicationVersion is not null)]);

        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
            {
                yield return Text.NewLine;
            }

            foreach (var renderable in sections[i])
            {
                yield return renderable;
            }
        }
    }

    private static Markup Heading(string text) => new($"[yellow]{Markup.Escape(text)}:[/]\n");

    private static string Usage(string applicationName, List<ICommandArgument> arguments)
    {
        var parts = new List<string> { Markup.Escape(applicationName) };
        parts.AddRange(arguments.Select(argument => argument.IsRequired
            ? $"[aqua]{Markup.Escape(Name(argument))}[/]"
            : $"[silver]{Markup.Escape(Name(argument))}[/]"));
        parts.Add($"[grey]{Markup.Escape($"[{Strings.HelpOptions}]")}[/]");
        return string.Join(' ', parts);
    }

    private static Grid Arguments(List<ICommandArgument> arguments)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().Padding(4, 0, 4, 0))
            .AddColumn(new GridColumn().Padding(0, 0, 0, 0));

        foreach (var argument in arguments)
        {
            grid.AddRow($"[silver]{Markup.Escape(Name(argument))}[/]", Description(argument.Description));
        }

        return grid;
    }

    private static Grid Options(List<ICommandOption> options, bool showVersion)
    {
        var rows = new List<(string Names, string Default, string Description)>
        {
            ("-h, --help", "", Description(Strings.HelpPrintsHelp)),
        };

        if (showVersion)
        {
            rows.Add(("-v, --version", "", Description(Strings.HelpPrintsVersion)));
        }

        foreach (var option in options)
        {
            var names = option.ShortNames.Select(name => "-" + name).Concat(option.LongNames.Select(name => "--" + name));
            var value = option.IsFlag || option.ValueName is null ? ""
                : option.ValueIsOptional ? $" [grey]{Markup.Escape($"[{option.ValueName}]")}[/]"
                : $" [silver]{Markup.Escape($"<{option.ValueName}>")}[/]";

            var defaultValue = option.DefaultValue?.Value is { } valueObject
                ? $"[bold]{Markup.Escape(Convert.ToString(valueObject, CultureInfo.CurrentCulture) ?? "")}[/]"
                : "";

            rows.Add((Markup.Escape(string.Join(", ", names)) + value, defaultValue, Description(option.Description)));
        }

        // The DEFAULT column is only there when an option has a default value.
        var withDefaults = rows.Any(row => row.Default.Length > 0);

        var grid = new Grid().AddColumn(new GridColumn().Padding(4, 0, 4, 0));
        if (withDefaults)
        {
            grid.AddColumn(new GridColumn().Padding(0, 0, 4, 0));
        }

        grid.AddColumn(new GridColumn().Padding(0, 0, 0, 0));

        if (withDefaults)
        {
            grid.AddRow("", $"[lime]{Markup.Escape(Strings.HelpDefault)}[/]", "");
        }

        foreach (var (names, defaultValue, description) in rows)
        {
            grid.AddRow(withDefaults ? [names, defaultValue, description] : [names, description]);
        }

        return grid;
    }

    private static string Name(ICommandArgument argument) =>
        argument.IsRequired ? $"<{argument.Value}>" : $"[{argument.Value}]";

    private static string Description(string? text) => Markup.Escape(Normalize(text ?? ""));

    /// <summary>Like Spectre's help: one sentence-ending period is dropped.</summary>
    private static string Normalize(string text)
    {
        text = text.Trim();
        return text.EndsWith('.') ? text[..^1] : text;
    }
}
