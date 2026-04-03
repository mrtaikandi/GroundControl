using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000, CA2213 // Terminal.Gui containers dispose their children

internal sealed class ResourceDetailView<T> : FrameView
{
    private readonly ResourceViewModel<T> _viewModel;
    private readonly IApplication _app;
    private readonly Label _contentLabel;

    public ResourceDetailView(ResourceViewModel<T> viewModel, IApplication app)
    {
        _viewModel = viewModel;
        _app = app;
        Title = "Details";
        X = Pos.Percent(40);
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _contentLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            Text = "Select an item to view details."
        };

        Add(_contentLabel);

        _viewModel.SelectedItemChanged += OnSelectedItemChanged;
    }

    private void OnSelectedItemChanged()
    {
        _app.Invoke(() =>
        {
            if (_viewModel.SelectedItem is not { } item)
            {
                _contentLabel.Text = "Select an item to view details.";
                return;
            }

            var pairs = _viewModel.GetDetailPairs(item);
            var maxKeyLength = 0;
            foreach (var pair in pairs)
            {
                if (pair.Key.Length > maxKeyLength)
                {
                    maxKeyLength = pair.Key.Length;
                }
            }

            var lines = new string[pairs.Count];
            for (var i = 0; i < pairs.Count; i++)
            {
                lines[i] = $"{pairs[i].Key.PadRight(maxKeyLength)}  {pairs[i].Value}";
            }

            _contentLabel.Text = string.Join(Environment.NewLine, lines);
        });
    }
}