using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VChibi.Models;
using VChibi.Services;

namespace VChibi.ViewModels;

public partial class PetEditorViewModel : ObservableObject
{
    private readonly PetStore _petStore;
    private readonly IFilePickerService _filePicker;

    [ObservableProperty]
    private string? _existingPetId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationSummary))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFps))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationSummary))]
    private int _idleFps = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFps))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationSummary))]
    private int _walkFps = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdleSelected))]
    [NotifyPropertyChangedFor(nameof(IsWalkSelected))]
    [NotifyPropertyChangedFor(nameof(CurrentFrames))]
    [NotifyPropertyChangedFor(nameof(CurrentFps))]
    private string _selectedAction = "idle";

    [ObservableProperty]
    private ObservableCollection<PetFrameItemViewModel> _idleFrames = new();

    [ObservableProperty]
    private ObservableCollection<PetFrameItemViewModel> _walkFrames = new();

    [ObservableProperty]
    private PetFrameItemViewModel? _selectedFrame;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isPreviewFlipped;

    [ObservableProperty]
    private bool _isPreviewPlaying = true;

    public bool IsIdleSelected
    {
        get => string.Equals(SelectedAction, "idle", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value && !IsIdleSelected)
            {
                SelectAction("idle");
            }
        }
    }

    public bool IsWalkSelected
    {
        get => string.Equals(SelectedAction, "walk", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value && !IsWalkSelected)
            {
                SelectAction("walk");
            }
        }
    }

    public ObservableCollection<PetFrameItemViewModel> CurrentFrames =>
        IsWalkSelected ? WalkFrames : IdleFrames;

    public int CurrentFps
    {
        get => IsWalkSelected ? WalkFps : IdleFps;
        set
        {
            int clamped = Math.Clamp(value, 1, 60);
            if (IsWalkSelected)
            {
                if (WalkFps != clamped)
                {
                    WalkFps = clamped;
                    IsDirty = true;
                    RequestFpsUpdate?.Invoke(clamped);
                }
            }
            else
            {
                if (IdleFps != clamped)
                {
                    IdleFps = clamped;
                    IsDirty = true;
                    RequestFpsUpdate?.Invoke(clamped);
                }
            }
        }
    }

    public long CurrentTotalPngBytes =>
        IdleFrames.Sum(f => f.Preflight.StreamLength) + WalkFrames.Sum(f => f.Preflight.StreamLength);

    public long CurrentTotalPbgra32Bytes =>
        IdleFrames.Sum(f => f.Preflight.Pbgra32Bytes) + WalkFrames.Sum(f => f.Preflight.Pbgra32Bytes);

    public long CurrentTotalNativePeakBytes =>
        IdleFrames.Sum(f => f.Preflight.NativePeakBytes) + WalkFrames.Sum(f => f.Preflight.NativePeakBytes);

    public bool CanSave =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Name) &&
        IdleFrames.Count > 0 &&
        WalkFrames.Count > 0 &&
        IdleFps >= 1 && IdleFps <= 60 &&
        WalkFps >= 1 && WalkFps <= 60;

    public string ValidationSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "Vui lòng nhập tên cho pet.";
            if (IdleFrames.Count == 0) return "Cần ít nhất 1 frame cho hành động Đứng (Idle).";
            if (WalkFrames.Count == 0) return "Cần ít nhất 1 frame cho hành động Đi (Walk).";
            if (IdleFps < 1 || IdleFps > 60) return "FPS Đứng phải từ 1 đến 60.";
            if (WalkFps < 1 || WalkFps > 60) return "FPS Đi phải từ 1 đến 60.";
            return string.Empty;
        }
    }

    public Func<string, string, bool> ConfirmCancelAction { get; set; } = (msg, title) =>
        System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
        == System.Windows.MessageBoxResult.Yes;

    public event Action<PetDefinition, bool>? PetSaved;
    public event Action? Cancelled;
    public event Action<int, IReadOnlyList<BitmapSource>>? RequestPreviewUpdate;
    public event Action<int>? RequestFpsUpdate;
    public event Action<bool>? RequestFlipUpdate;

    public PetEditorViewModel(PetStore petStore, IFilePickerService filePicker)
    {
        _petStore = petStore;
        _filePicker = filePicker;
    }

    public void InitializeNew()
    {
        ExistingPetId = null;
        Name = "Idol mới";
        IdleFps = 8;
        WalkFps = 8;
        IdleFrames.Clear();
        WalkFrames.Clear();
        SelectedFrame = null;
        ErrorMessage = null;
        IsDirty = false;
        IsBusy = false;
        SelectedAction = "idle";
        TriggerPreviewUpdate();
    }

    public void InitializeFromPet(PetDefinition pet)
    {
        ExistingPetId = pet.Id;
        Name = pet.Name;
        IdleFrames.Clear();
        WalkFrames.Clear();
        ErrorMessage = null;
        IsBusy = false;

        // Load idle frames
        if (pet.Animations.TryGetValue("idle", out var idleAnim))
        {
            IdleFps = Math.Clamp(idleAnim.Fps, 1, 60);
            for (int i = 0; i < idleAnim.Frames.Count; i++)
            {
                var relPath = idleAnim.Frames[i];
                var fullPath = Path.Combine(pet.RootDirectory, relPath);
                if (PetValidator.TryValidateAndDecodePng(
                    fullPath,
                    null,
                    CurrentTotalPngBytes,
                    CurrentTotalPbgra32Bytes,
                    CurrentTotalNativePeakBytes,
                    out var bmp,
                    out var preflight,
                    out _))
                {
                    IdleFrames.Add(new PetFrameItemViewModel(fullPath, bmp!, IdleFrames.Count + 1, preflight));
                }
            }
        }
        else
        {
            IdleFps = 8;
        }

        // Load walk frames
        if (pet.Animations.TryGetValue("walk", out var walkAnim))
        {
            WalkFps = Math.Clamp(walkAnim.Fps, 1, 60);
            for (int i = 0; i < walkAnim.Frames.Count; i++)
            {
                var relPath = walkAnim.Frames[i];
                var fullPath = Path.Combine(pet.RootDirectory, relPath);
                if (PetValidator.TryValidateAndDecodePng(
                    fullPath,
                    null,
                    CurrentTotalPngBytes,
                    CurrentTotalPbgra32Bytes,
                    CurrentTotalNativePeakBytes,
                    out var bmp,
                    out var preflight,
                    out _))
                {
                    WalkFrames.Add(new PetFrameItemViewModel(fullPath, bmp!, WalkFrames.Count + 1, preflight));
                }
            }
        }
        else
        {
            WalkFps = 8;
        }

        SelectedAction = "idle";
        SelectedFrame = IdleFrames.FirstOrDefault();
        IsDirty = false;
        TriggerPreviewUpdate();
    }

    partial void OnNameChanged(string value)
    {
        IsDirty = true;
    }

    partial void OnSelectedActionChanged(string value)
    {
        SelectedFrame = CurrentFrames.FirstOrDefault();
        TriggerPreviewUpdate();
    }

    partial void OnIsPreviewFlippedChanged(bool value)
    {
        RequestFlipUpdate?.Invoke(value);
    }

    [RelayCommand]
    public void SelectAction(string action)
    {
        if (string.Equals(action, "walk", StringComparison.OrdinalIgnoreCase))
        {
            SelectedAction = "walk";
        }
        else
        {
            SelectedAction = "idle";
        }
    }

    [RelayCommand]
    public void AddFrames()
    {
        var actionLabel = IsWalkSelected ? "Đi (Walk)" : "Đứng (Idle)";
        var files = _filePicker.PickPngFiles($"Chọn ảnh PNG cho hành động {actionLabel}");
        if (files == null || files.Count == 0) return;

        // Apply natural initial ordering
        var sorted = files
            .OrderBy(f => Path.GetFileName(f), StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, true))
            .OrderBy(f => Path.GetFileName(f), new NaturalComparer())
            .ToList();

        int addedCount = 0;
        string? lastError = null;

        foreach (var file in sorted)
        {
            if (CurrentFrames.Count >= PetValidator.MaxFramesPerAction)
            {
                lastError = $"Số lượng frame cho hành động này đã đạt giới hạn tối đa ({PetValidator.MaxFramesPerAction}).";
                break;
            }

            if (PetValidator.TryValidateAndDecodePng(
                file,
                null,
                CurrentTotalPngBytes,
                CurrentTotalPbgra32Bytes,
                CurrentTotalNativePeakBytes,
                out var bmp,
                out var preflight,
                out var error))
            {
                CurrentFrames.Add(new PetFrameItemViewModel(file, bmp!, CurrentFrames.Count + 1, preflight));
                addedCount++;
            }
            else
            {
                lastError = error;
            }
        }

        if (addedCount > 0)
        {
            IsDirty = true;
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(ValidationSummary));
            TriggerPreviewUpdate();
        }

        ErrorMessage = lastError;
    }

    [RelayCommand]
    public void RemoveFrame()
    {
        if (SelectedFrame == null) return;

        int index = CurrentFrames.IndexOf(SelectedFrame);
        if (index < 0) return;

        CurrentFrames.RemoveAt(index);
        ReindexFrames();

        if (CurrentFrames.Count > 0)
        {
            int newIndex = Math.Min(index, CurrentFrames.Count - 1);
            SelectedFrame = CurrentFrames[newIndex];
        }
        else
        {
            SelectedFrame = null;
        }

        IsDirty = true;
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ValidationSummary));
        TriggerPreviewUpdate();
    }

    [RelayCommand]
    public void MoveFrameUp()
    {
        if (SelectedFrame == null) return;
        int index = CurrentFrames.IndexOf(SelectedFrame);
        if (index <= 0) return;

        CurrentFrames.Move(index, index - 1);
        ReindexFrames();
        IsDirty = true;
        TriggerPreviewUpdate();
    }

    [RelayCommand]
    public void MoveFrameDown()
    {
        if (SelectedFrame == null) return;
        int index = CurrentFrames.IndexOf(SelectedFrame);
        if (index < 0 || index >= CurrentFrames.Count - 1) return;

        CurrentFrames.Move(index, index + 1);
        ReindexFrames();
        IsDirty = true;
        TriggerPreviewUpdate();
    }

    [RelayCommand]
    public void TogglePlayPreview()
    {
        IsPreviewPlaying = !IsPreviewPlaying;
    }

    [RelayCommand]
    public void Save()
    {
        ExecuteSave(andActivate: false);
    }

    [RelayCommand]
    public void SaveAndActivate()
    {
        ExecuteSave(andActivate: true);
    }

    private void ExecuteSave(bool andActivate)
    {
        if (!CanSave) return;

        IsBusy = true;
        ErrorMessage = null;
        PetDefinition committedDef;

        try
        {
            var actions = new Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)>
            {
                ["idle"] = (IdleFps, IdleFrames.Select(f => f.FilePath).ToList()),
                ["walk"] = (WalkFps, WalkFrames.Select(f => f.FilePath).ToList())
            };

            committedDef = _petStore.SavePetDraft(ExistingPetId, Name.Trim(), actions);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi khi lưu pet: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // OUTSIDE save catch block: downstream activation errors will never be mislabeled as save failure
        try
        {
            PetSaved?.Invoke(committedDef, andActivate);
        }
        catch
        {
            // Any unhandled UI error from listener should not alter saved state
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        if (IsDirty)
        {
            bool confirm = ConfirmCancelAction(
                "Bạn có các thay đổi chưa lưu trong editor. Bạn có chắc chắn muốn hủy và quay lại thư viện không?",
                "Xác nhận hủy thay đổi"
            );
            if (!confirm) return;
        }

        Cancelled?.Invoke();
    }

    private void ReindexFrames()
    {
        for (int i = 0; i < CurrentFrames.Count; i++)
        {
            CurrentFrames[i].Index = i + 1;
        }
    }

    public void TriggerPreviewUpdate()
    {
        var frames = CurrentFrames.Select(f => f.Thumbnail).ToList();
        RequestPreviewUpdate?.Invoke(CurrentFps, frames);
    }

    private class NaturalComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => NaturalSortHelper.Compare(x, y);
    }
}
