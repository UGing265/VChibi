using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VChibi.ViewModels;

namespace VChibi.Views;

public partial class LibraryWindow : Window
{
    private bool _isRealShutdown;
    private LibraryViewModel? _viewModel;

    private string CurrentAction => (WalkActionRadio?.IsChecked == true) ? "walk" : "idle";

    public LibraryWindow()
    {
        InitializeComponent();
    }

    public LibraryWindow(LibraryViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PreviewPetChanged += OnPreviewPetChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPet != null)
        {
            PetPreview?.LoadPet(_viewModel.SelectedPet.Definition, CurrentAction);
        }
    }

    private void OnPreviewPetChanged(Models.PetDefinition? pet)
    {
        PetPreview?.LoadPet(pet, CurrentAction);
    }

    private void OnPreviewActionChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            string action = rb == WalkActionRadio ? "walk" : "idle";
            PetPreview?.SetAction(action);
        }
    }

    public void AllowClose()
    {
        _isRealShutdown = true;
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        PetPreview?.Play();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isRealShutdown)
        {
            e.Cancel = true;
            Hide();
            PetPreview?.Stop();
            return;
        }

        base.OnClosing(e);
    }
}
