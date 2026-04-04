using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000 // Terminal.Gui containers dispose their children

internal sealed class DeleteConfirmationDialog
{
    private readonly IApplication _app;

    public DeleteConfirmationDialog(IApplication app)
    {
        _app = app;
    }

    public bool Show(string resourceType, string resourceName)
    {
        using var dialog = new Dialog
        {
            Title = "Confirm Delete",
            Width = 50,
            Height = 8
        };

        var messageLabel = new Label
        {
            Text = $"Delete {resourceType} \"{resourceName}\"?",
            X = 1,
            Y = 1,
            Width = Dim.Fill(2)
        };

        var warningLabel = new Label
        {
            Text = "This action cannot be undone.",
            X = 1,
            Y = 3
        };

        dialog.Add(messageLabel);
        dialog.Add(warningLabel);

        var confirmed = false;

        var deleteButton = new Button
        {
            Text = "Delete"
        };

        deleteButton.Accepting += (_, _) =>
        {
            confirmed = true;
            _app.RequestStop(dialog);
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            IsDefault = true
        };

        cancelButton.Accepting += (_, _) => _app.RequestStop(dialog);

        dialog.AddButton(deleteButton);
        dialog.AddButton(cancelButton);
        _app.Run(dialog);

        return confirmed;
    }
}