using System.Text;
using Spectre.Console;

namespace WorkspaceOrchestrator.Cli.Commands;

/// <summary>
/// A TextWriter that routes each line to the Spectre.Console StatusContext spinner
/// so that service step messages appear as spinner status updates.
/// </summary>
internal sealed class SpinnerWriter : TextWriter
{
    private readonly StatusContext _ctx;

    public SpinnerWriter(StatusContext ctx) => _ctx = ctx;

    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _ctx.Status(Markup.Escape(value.Trim()));
    }

    public override void Write(string? value) { /* ignore partial writes */ }
    public override void Write(char value)    { /* ignore partial writes */ }
}
