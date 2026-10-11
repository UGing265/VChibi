using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VChibi.Models;
using VChibi.Runtime;
using VChibi.Services;

namespace VChibi.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly PetStore _petStore;
    private readonly SettingsStore _settingsStore;
    private readonly DesktopPetSession _desktopSession;
    private readonly IFilePickerService _filePicker;
    private AppSettings _settings = new();
    private bool _isUpdatingInternally;

    [ObservableProperty]
    private string _title = "VChibi — Desktop Idol Pet";

    [ObservableProperty]
    private string _statusText = "Sẵn sàng";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _emptyStateText = "Chưa có pet nào trong thư viện.";

    [ObservableProperty]
    private ObservableCollection<PetListItemViewModel> _pets = new();

    [ObservableProperty]
    private PetListItemViewModel? _selectedPet;

    [ObservableProperty]
    private string? _activePetId;

    [ObservableProperty]
    private bool _isDesktopPetVisible = true;

    [ObservableProperty]
    private double _scalePercent = PetDisplaySettings.DefaultScale;

    [ObservableProperty]
    private double _movementSpeed = PetDisplaySettings.DefaultSpeed;

    [ObservableProperty]
    private bool _isSettingsCorrupted;

    [ObservableProperty]
    private string? _corruptionErrorMessage;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private PetEditorViewModel? _editor;

    public event Action<PetDefinition?>? PreviewPetChanged;
    public event Action? RequestShutdown;

    public Action<string, string> ShowErrorAction { get; set; } = (message, title) =>
    {
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error
        );
    };

    public Func<string, string, bool> ConfirmResetAction { get; set; } = (message, title) =>
    {
        return System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning
        ) == System.Windows.MessageBoxResult.Yes;
    };

    public LibraryViewModel(PetStore petStore, SettingsStore settingsStore, DesktopPetSession desktopSession, IFilePickerService? filePicker = null)
    {
        _petStore = petStore;
        _settingsStore = settingsStore;
        _desktopSession = desktopSession;
        _filePicker = filePicker ?? new WindowsFilePickerService();
    }


    public void Initialize()
    {
        IsLoading = true;
        try
        {
            var (settings, result, error) = _settingsStore.Load();
            _settings = settings;

            if (result == SettingsLoadResult.Corrupt)
            {
                IsSettingsCorrupted = true;
                CorruptionErrorMessage = error ?? "Tệp cài đặt settings.json bị lỗi cấu trúc hoặc giá trị không hợp lệ.";
                StatusText = "Cảnh báo: Tệp cài đặt bị lỗi, đang tạm dừng tự động lưu.";
            }

            ReloadPets();

            // Restore active pet
            var activeId = _settings.ActivePetId ?? "default-chibi";
            bool activePackageMissing = _settings.ActivePetId != null &&
                                         !Pets.Any(p => string.Equals(p.Id, _settings.ActivePetId, StringComparison.OrdinalIgnoreCase));
            var activeItem = Pets.FirstOrDefault(p => string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase))
                             ?? Pets.FirstOrDefault();
            bool activationFailed = false;

            if (activeItem != null)
            {
                ActivePetId = activeItem.Id;
                foreach (var pet in Pets)
                {
                    pet.IsActive = string.Equals(pet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase);
                }

                // Restore active pet display settings
                var activeSettings = _settings.GetOrCreatePetSettings(activeItem.Id);
                _desktopSession.SetDisplaySettings(activeSettings.ScalePercent, activeSettings.MovementSpeed);
                bool activated = _desktopSession.TryActivatePet(activeItem.Definition);

                if (activated)
                {
                    if (_settings.IsPetVisible)
                    {
                        _desktopSession.Show();
                        IsDesktopPetVisible = true;
                    }
                    else
                    {
                        _desktopSession.Hide();
                        IsDesktopPetVisible = false;
                    }
                }
                else
                {
                    activationFailed = true;
                    _desktopSession.Hide();
                    IsDesktopPetVisible = false;
                }
            }
            else
            {
                _desktopSession.Hide();
                IsDesktopPetVisible = false;
            }

            // Restore selected pet
            var selectedId = _settings.SelectedPetId ?? ActivePetId;
            var selectedItem = Pets.FirstOrDefault(p => string.Equals(p.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                               ?? activeItem;
            SelectedPet = selectedItem;

            if (activationFailed && activeItem != null)
            {
                StatusText = $"Cảnh báo: Không thể tải gói pet '{activeItem.Name}' ({activeItem.Id}). Pet đang ở trạng thái ẩn.";
            }
            else if (result == SettingsLoadResult.Corrupt)
            {
                // Keep corruption warning from above
            }
            else if (Pets.Count == 0)
            {
                StatusText = "Thư viện hiện chưa có pet nào khả dụng.";
            }
            else if (activePackageMissing)
            {
                StatusText = $"Gói pet '{_settings.ActivePetId}' không còn trong thư viện. Đã chọn '{activeItem?.Name}'.";
            }
            else
            {
                StatusText = $"Đã tải {Pets.Count} pet vào thư viện";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ReloadPets()
    {
        var allPets = _petStore.GetAllPets();
        var sorted = allPets.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                            .ThenBy(p => p.Id, StringComparer.Ordinal)
                            .ToList();

        Pets.Clear();
        foreach (var def in sorted)
        {
            bool isActive = string.Equals(def.Id, ActivePetId, StringComparison.OrdinalIgnoreCase);
            Pets.Add(new PetListItemViewModel(def, isActive));
        }

        EmptyStateText = Pets.Count == 0 ? "Chưa có pet nào trong thư viện." : "";
    }

    partial void OnSelectedPetChanged(PetListItemViewModel? value)
    {
        if (value == null)
        {
            PreviewPetChanged?.Invoke(null);
            return;
        }

        _isUpdatingInternally = true;
        try
        {
            var petSettings = _settings.GetOrCreatePetSettings(value.Id);
            ScalePercent = petSettings.ScalePercent;
            MovementSpeed = petSettings.MovementSpeed;
        }
        finally
        {
            _isUpdatingInternally = false;
        }

        _settings.SelectedPetId = value.Id;
        TrySaveSettings("chọn pet");

        PreviewPetChanged?.Invoke(value.Definition);
    }

    partial void OnScalePercentChanged(double value)
    {
        if (_isUpdatingInternally || SelectedPet == null) return;

        double clamped = Math.Clamp(value, PetDisplaySettings.MinScale, PetDisplaySettings.MaxScale);
        var petSettings = _settings.GetOrCreatePetSettings(SelectedPet.Id);
        petSettings.ScalePercent = clamped;
        TrySaveSettings("thay đổi kích thước");

        // If selected pet is currently the active desktop pet, apply immediately
        if (string.Equals(SelectedPet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase))
        {
            _desktopSession.SetDisplaySettings(clamped, MovementSpeed);
        }
    }

    partial void OnMovementSpeedChanged(double value)
    {
        if (_isUpdatingInternally || SelectedPet == null) return;

        double clamped = Math.Clamp(value, PetDisplaySettings.MinSpeed, PetDisplaySettings.MaxSpeed);
        var petSettings = _settings.GetOrCreatePetSettings(SelectedPet.Id);
        petSettings.MovementSpeed = clamped;
        TrySaveSettings("thay đổi tốc độ");

        // If selected pet is currently the active desktop pet, apply immediately
        if (string.Equals(SelectedPet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase))
        {
            _desktopSession.SetDisplaySettings(ScalePercent, clamped);
        }
    }

    [RelayCommand]
    private void ReleaseToDesktop()
    {
        if (SelectedPet == null) return;

        bool wasHidden = !IsDesktopPetVisible;

        // Review fix F4: if hidden, activating selected ID if different; if load fails keep hidden
        if (!string.Equals(SelectedPet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase) || wasHidden)
        {
            bool success = _desktopSession.TryActivatePet(SelectedPet.Definition);
            if (!success)
            {
                if (wasHidden)
                {
                    _desktopSession.Hide();
                    IsDesktopPetVisible = false;
                }
                StatusText = $"Lỗi khi tải pet {SelectedPet.Name}";
                return;
            }

            ActivePetId = SelectedPet.Id;
            foreach (var pet in Pets)
            {
                pet.IsActive = string.Equals(pet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase);
            }

            var petSettings = _settings.GetOrCreatePetSettings(SelectedPet.Id);
            _desktopSession.SetDisplaySettings(petSettings.ScalePercent, petSettings.MovementSpeed);
        }

        _desktopSession.Show();
        IsDesktopPetVisible = true;

        _settings.ActivePetId = ActivePetId;
        _settings.IsPetVisible = true;
        TrySaveSettings("thả pet ra desktop");

        StatusText = $"Đã thả {SelectedPet.Name} ra desktop";
    }

    [RelayCommand]
    private void HideFromDesktop()
    {
        _desktopSession.Hide();
        IsDesktopPetVisible = false;

        _settings.IsPetVisible = false;
        TrySaveSettings("ẩn pet khỏi desktop");

        StatusText = "Đã ẩn pet khỏi desktop";
    }

    [RelayCommand]
    private void ToggleDesktopPet()
    {
        if (IsDesktopPetVisible)
        {
            HideFromDesktop();
        }
        else
        {
            ReleaseToDesktop();
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        bool confirmed = ConfirmResetAction(
            "Tệp cài đặt hiện tại đang bị lỗi. Bạn có chắc chắn muốn đặt lại tất cả cài đặt về mặc định và ghi đè không?",
            "Xác nhận đặt lại cài đặt"
        );

        if (!confirmed)
        {
            StatusText = "Đã hủy thao tác đặt lại cài đặt.";
            return;
        }
        var defaultSettings = new AppSettings();
        _settingsStore.IsPersistenceEnabled = true;
        bool saved = _settingsStore.Save(defaultSettings);

        if (!saved)
        {
            _settingsStore.IsPersistenceEnabled = false;
            IsSettingsCorrupted = true;
            StatusText = "Lỗi: Không thể lưu cài đặt mặc định. Vui lòng thử lại hoặc thoát không lưu.";
            return;
        }

        _settings = defaultSettings;
        IsSettingsCorrupted = false;
        StatusText = "Đã đặt lại cài đặt về mặc định";
        if (SelectedPet != null)
        {
            var petSettings = _settings.GetOrCreatePetSettings(SelectedPet.Id);
            _isUpdatingInternally = true;
            try
            {
                ScalePercent = petSettings.ScalePercent;
                MovementSpeed = petSettings.MovementSpeed;
            }
            finally
            {
                _isUpdatingInternally = false;
            }

            if (string.Equals(SelectedPet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase))
            {
                _desktopSession.SetDisplaySettings(ScalePercent, MovementSpeed);
            }
        }
    }

    [RelayCommand]
    private void CancelSettingsRecovery()
    {
        _settingsStore.IsPersistenceEnabled = false;
        Exit();
    }

    private void TrySaveSettings(string actionDescription)
    {
        if (IsSettingsCorrupted || !_settingsStore.IsPersistenceEnabled)
        {
            return;
        }

        bool success = _settingsStore.Save(_settings);
        if (!success)
        {
            StatusText = $"Cảnh báo: Không thể lưu cài đặt ({actionDescription})";
        }
    }

    [RelayCommand]
    public void OpenCreatePet()
    {
        var editorVm = new PetEditorViewModel(_petStore, _filePicker);
        editorVm.PetSaved += OnEditorPetSaved;
        editorVm.Cancelled += OnEditorCancelled;
        editorVm.InitializeNew();
        Editor = editorVm;
        IsEditorOpen = true;
        StatusText = "Đang tạo pet mới...";
    }

    [RelayCommand]
    public void OpenEditPet()
    {
        if (SelectedPet == null) return;
        var editorVm = new PetEditorViewModel(_petStore, _filePicker);
        editorVm.PetSaved += OnEditorPetSaved;
        editorVm.Cancelled += OnEditorCancelled;
        editorVm.InitializeFromPet(SelectedPet.Definition);
        Editor = editorVm;
        IsEditorOpen = true;
        StatusText = $"Đang chỉnh sửa pet '{SelectedPet.Name}'...";
    }

    [RelayCommand]
    public void ImportPackage()
    {
        var folder = _filePicker.PickFolder("Chọn thư mục gói pet cần import");
        if (string.IsNullOrWhiteSpace(folder)) return;

        try
        {
            StatusText = "Đang kiểm tra và nhập gói pet...";
            var imported = _petStore.ImportPackage(folder);
            ReloadPets();
            var importedItem = Pets.FirstOrDefault(p => string.Equals(p.Id, imported.Id, StringComparison.OrdinalIgnoreCase));
            if (importedItem != null)
            {
                SelectedPet = importedItem;
            }
            StatusText = $"Đã nhập thành công gói pet '{imported.Name}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Lỗi nhập gói: {ex.Message}";
            ShowErrorAction?.Invoke($"Không thể nhập gói pet từ thư mục '{folder}':\n\n{ex.Message}", "Lỗi nhập gói pet");
        }
    }

    private void OnEditorPetSaved(PetDefinition savedDef, bool andActivate)
    {
        IsEditorOpen = false;
        Editor = null;
        ReloadPets();

        var savedItem = Pets.FirstOrDefault(p => string.Equals(p.Id, savedDef.Id, StringComparison.OrdinalIgnoreCase));
        if (savedItem != null)
        {
            SelectedPet = savedItem;
        }

        if (andActivate)
        {
            try
            {
                bool activated = _desktopSession.TryActivatePet(savedDef);
                if (activated)
                {
                    ActivePetId = savedDef.Id;
                    foreach (var pet in Pets)
                    {
                        pet.IsActive = string.Equals(pet.Id, ActivePetId, StringComparison.OrdinalIgnoreCase);
                    }
                    var petSettings = _settings.GetOrCreatePetSettings(savedDef.Id);
                    _desktopSession.SetDisplaySettings(petSettings.ScalePercent, petSettings.MovementSpeed);
                    _desktopSession.Show();
                    IsDesktopPetVisible = true;
                    _settings.ActivePetId = ActivePetId;
                    _settings.IsPetVisible = true;
                    TrySaveSettings("thả pet mới ra desktop");
                    StatusText = $"Đã lưu và thả pet '{savedDef.Name}' ra desktop";
                }
                else
                {
                    StatusText = $"Đã lưu gói pet '{savedDef.Name}', nhưng không thể nạp lên desktop (kích hoạt thất bại).";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Đã lưu gói pet '{savedDef.Name}', nhưng không thể nạp lên desktop ({ex.Message}).";
            }
        }
        else
        {
            // Check if saved pet was the active desktop pet (A10)
            if (string.Equals(savedDef.Id, ActivePetId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool reloaded = _desktopSession.TryActivatePet(savedDef);
                    if (reloaded)
                    {
                        var petSettings = _settings.GetOrCreatePetSettings(savedDef.Id);
                        _desktopSession.SetDisplaySettings(petSettings.ScalePercent, petSettings.MovementSpeed);
                        StatusText = $"Đã lưu và cập nhật pet '{savedDef.Name}' đang chạy trên desktop";
                    }
                    else
                    {
                        StatusText = $"Đã lưu gói pet '{savedDef.Name}', nhưng chưa thể cập nhật pet đang chạy trên desktop.";
                    }
                }
                catch (Exception ex)
                {
                    StatusText = $"Đã lưu gói pet '{savedDef.Name}', nhưng chưa thể cập nhật pet đang chạy trên desktop ({ex.Message}).";
                }
            }
            else
            {
                StatusText = $"Đã lưu gói pet '{savedDef.Name}' thành công";
            }
        }
    }

    private void OnEditorCancelled()
    {
        IsEditorOpen = false;
        Editor = null;
        StatusText = "Đã hủy thao tác chỉnh sửa.";
    }

    [RelayCommand]
    private void Exit()
    {
        // Snapshot settings before shutdown only if not corrupt and persistence enabled
        if (!IsSettingsCorrupted && _settingsStore.IsPersistenceEnabled)
        {
            _settings.ActivePetId = ActivePetId;
            _settings.SelectedPetId = SelectedPet?.Id;
            _settings.IsPetVisible = IsDesktopPetVisible;
            _settingsStore.Save(_settings);
        }

        RequestShutdown?.Invoke();
    }
}
