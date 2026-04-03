using Terminal.Gui.App;
using Terminal.Gui.Input;
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

        var fieldWidgets = new List<(FieldDefinition Definition, View Widget)>();
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

            View widget;
            switch (field.Type)
            {
                case FieldType.Boolean:
                    var checkBox = new CheckBox
                    {
                        X = 1,
                        Y = y,
                        Text = field.Label,
                        Value = string.Equals(field.DefaultValue, "true", StringComparison.OrdinalIgnoreCase)
                            ? CheckState.Checked
                            : CheckState.UnChecked
                    };

                    widget = checkBox;
                    break;

                case FieldType.MultiLineText:
                    var textView = new TextView
                    {
                        X = 1,
                        Y = y,
                        Width = Dim.Fill(2),
                        Height = 3,
                        Text = field.DefaultValue
                    };

                    widget = textView;
                    y += 2;
                    break;

                default:
                    var textField = new TextField
                    {
                        X = 1,
                        Y = y,
                        Width = Dim.Fill(2),
                        Text = field.DefaultValue
                    };

                    widget = textField;
                    break;
            }

            dialog.Add(widget);
            fieldWidgets.Add((field, widget));
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
            confirmed = true;
            _app.RequestStop(dialog);
        };

        var cancelButton = new Button
        {
            Text = "Cancel"
        };

        cancelButton.Accepting += (_, _) => _app.RequestStop(dialog);

        dialog.Add(okButton);
        dialog.Add(cancelButton);
        _app.Run(dialog);

        if (!confirmed)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (definition, widget) in fieldWidgets)
        {
            var value = widget switch
            {
                TextField tf => tf.Text,
                TextView tv => tv.Text,
                CheckBox cb => cb.Value == CheckState.Checked ? "true" : "false",
                _ => string.Empty
            };

            result[definition.Label] = value;
        }

        return result;
    }
}