using System.Windows;
using System.Windows.Controls;
using VChibi.ViewModels;

namespace VChibi.Views;

public partial class PetEditorView : UserControl
{
    private PetEditorViewModel? _viewModel;

    public PetEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestPreviewUpdate -= OnRequestPreviewUpdate;
            _viewModel.RequestFpsUpdate -= OnRequestFpsUpdate;
            _viewModel.RequestFlipUpdate -= OnRequestFlipUpdate;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as PetEditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.RequestPreviewUpdate += OnRequestPreviewUpdate;
            _viewModel.RequestFpsUpdate += OnRequestFpsUpdate;
            _viewModel.RequestFlipUpdate += OnRequestFlipUpdate;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _viewModel.TriggerPreviewUpdate();
            EditorPreview?.SetHorizontalFlip(_viewModel.IsPreviewFlipped);
        }
        else
        {
            EditorPreview?.Stop();
        }
    }

    private void OnRequestPreviewUpdate(int fps, IReadOnlyList<System.Windows.Media.Imaging.BitmapSource> frames)
    {
        EditorPreview?.LoadDraftFrames(fps, frames);
    }

    private void OnRequestFpsUpdate(int fps)
    {
        EditorPreview?.UpdateFps(fps);
    }

    private void OnRequestFlipUpdate(bool flip)
    {
        EditorPreview?.SetHorizontalFlip(flip);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel == null || EditorPreview == null) return;

        if (e.PropertyName == nameof(PetEditorViewModel.IsPreviewPlaying))
        {
            if (_viewModel.IsPreviewPlaying)
            {
                EditorPreview.Play();
            }
            else
            {
                EditorPreview.Stop();
            }
        }
    }
}
