using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000 // Terminal.Gui containers dispose their children

internal sealed class ResourceFormDialog
{
    private readonly IApplication _app;

    public ResourceFormDialog(IApplication app)
    {
        _app = app;
    }

    public Dictionary<string, string>? Show(string title, IReadOnlyList<FieldDefinition> fields)
    {
        using var dialog = new Dialog
        {
            Title = title,
            Width = 60,
            Height = fields.Count * 3 + 6
        };

        var fieldWidgets = new List<(FieldDefinition Definition, TextField Widget)>();
        var y = 0;

        foreach (var field in fields)
        {
            var requiredMark = field.IsRequired ? " *" : "";
            var label = new Label
            {
                Text = $"{field.Label}{requiredMark}:",
                X = 1,
                Y = y
            };

            dialog.Add(label);
            y++;

            var textField = new TextField
            {
                X = 1,
                Y = y,
                Width = Dim.Fill(2),
                Text = field.DefaultValue
            };

            dialog.Add(textField);
            fieldWidgets.Add((field, textField));
            y += 2;
        }

        var confirmed = false;

        var okButton = new Button
        {
            Text = "OK",
            IsDefault = true
        };

        okButton.Accepting += (_, _) =>
        {
            foreach (var (definition, widget) in fieldWidgets)
            {
                if (definition.IsRequired && string.IsNullOrWhiteSpace(widget.Text))
                {
                    MessageBox.ErrorQuery(_app, "Validation", $"{definition.Label} is required.", "OK");
                    widget.SetFocus();
                    return;
                }
            }

            confirmed = true;
            _app.RequestStop(dialog);
        };

        var cancelButton = new Button
        {
            Text = "Cancel"
        };

        cancelButton.Accepting += (_, _) => _app.RequestStop(dialog);

        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        _app.Run(dialog);

        if (!confirmed)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (definition, widget) in fieldWidgets)
        {
            result[definition.Label] = widget.Text;
        }

        return result;
    }
}