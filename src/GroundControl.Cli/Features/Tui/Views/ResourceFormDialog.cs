#pragma warning disable CA2000 // Terminal.Gui containers dispose their children

using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;


internal sealed class ResourceFormDialog
{
    private readonly IApplication _app;

    public ResourceFormDialog(IApplication app)
    {
        _app = app;
    }

    public Dictionary<string, string>? Show(string title, IReadOnlyList<FieldDefinition> fields)
    {
        using var dialog = new Dialog();

        dialog.Title = title;
        dialog.Width = 60;
        dialog.Height = fields.Count * 3 + 7;

        var fieldWidgets = new List<(FieldDefinition Definition, TextField Widget)>();
        var y = 0;

        foreach (var field in fields)
        {
            var requiredMark = field.IsRequired ? " *" : string.Empty;
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

        var errorLabel = new Label
        {
            X = 1,
            Y = y,
            Width = Dim.Fill(2),
            Height = 1,
            Text = string.Empty
        };

        dialog.Add(errorLabel);

        var confirmed = false;

        // Intercept Enter on each text field to prevent Accept from bubbling to Dialog
        foreach (var (_, widget) in fieldWidgets)
        {
            widget.Accepting += (_, args) =>
            {
                args.Handled = true;
                ValidateAndSubmit();
            };
        }

        var okButton = new Button
        {
            Text = "OK"
        };

        okButton.Accepting += (_, args) =>
        {
            args.Handled = true;
            ValidateAndSubmit();
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

        void ValidateAndSubmit()
        {
            foreach (var (definition, widget) in fieldWidgets)
            {
                if (!definition.IsRequired || !string.IsNullOrWhiteSpace(widget.Text))
                {
                    continue;
                }

                errorLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(ColorName16.BrightRed, ColorName16.Gray)));
                errorLabel.Text = $"{definition.Label} is required.";
                widget.SetFocus();

                return;
            }

            confirmed = true;
            _app.RequestStop(dialog);
        }
    }
}