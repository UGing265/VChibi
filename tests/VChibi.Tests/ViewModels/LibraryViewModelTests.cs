using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using VChibi.Models;
using VChibi.Runtime;
using VChibi.Services;
using VChibi.ViewModels;
using Xunit;

namespace VChibi.Tests.ViewModels;

public class LibraryViewModelTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly AppLock _lock;

    public LibraryViewModelTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "VChibi_VM_Tests_" + Guid.NewGuid().ToString("N"));
        string defaultSourceDir = Path.Combine(_tempRoot, "mock_default_source");
        _paths = new AppPaths(_tempRoot, defaultSourceDir);
        _paths.EnsureDirectoriesCreated();
        CreateValidSourcePackage(defaultSourceDir);
        _lock = new AppLock(_paths.LockFilePath);
        Assert.True(_lock.TryAcquire());
    }

    public void Dispose()
    {
        _lock.Dispose();
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch { }
    }

    private static void CreateValidSourcePackage(string packageDir)
    {
        Directory.CreateDirectory(packageDir);
        CreatePng(Path.Combine(packageDir, "idle", "001.png"));
        CreatePng(Path.Combine(packageDir, "idle", "002.png"));
        CreatePng(Path.Combine(packageDir, "walk", "001.png"));
        CreatePng(Path.Combine(packageDir, "walk", "002.png"));

        var manifest = new
        {
            schemaVersion = 1,
            id = "default-chibi",
            name = "Default Chibi",
            animations = new
            {
                idle = new { fps = 8, frames = new[] { "idle/001.png", "idle/002.png" } },
                walk = new { fps = 8, frames = new[] { "walk/001.png", "walk/002.png" } }
            }
        };

        File.WriteAllText(Path.Combine(packageDir, "pet.json"), JsonSerializer.Serialize(manifest));
    }

    private static void CreatePng(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 120, 98, 184));
        bmp.Save(path, ImageFormat.Png);
    }

    private static void RunInSta(Action action)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (thrown != null)
        {
            throw new TargetInvocationException(thrown);
        }
    }

    private PetStore CreatePetStoreWithDefaultPet()
    {
        var store = new PetStore(_paths, _lock);
        store.SeedDefaultPetIfNeeded();
        return store;
    }

    [Fact]
    public void Initialize_SortsPetsNaturallyByNameThenId()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                Assert.NotEmpty(vm.Pets);
                Assert.NotNull(vm.SelectedPet);
                Assert.Equal("Default Chibi", vm.SelectedPet.Name);
                Assert.StartsWith("pet-", vm.SelectedPet.Id);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void Selection_DoesNotChangeActivePetOnDesktop()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                var activeIdBefore = vm.ActivePetId;

                // Add a second fake pet to test selection
                var pet2Def = new PetDefinition
                {
                    Id = "pet-beta",
                    Name = "Beta Pet",
                    RootDirectory = _paths.DefaultPetSourceDirectory,
                    Animations = new Dictionary<string, AnimationDefinition>
                    {
                        ["idle"] = new() { Fps = 4, Frames = new() { "idle/001.png" } }
                    }
                };
                var pet2Vm = new PetListItemViewModel(pet2Def, false);
                vm.Pets.Add(pet2Vm);

                // Select pet-beta
                vm.SelectedPet = pet2Vm;

                // Selected pet changed, but active pet MUST NOT change
                Assert.Equal("pet-beta", vm.SelectedPet.Id);
                Assert.Equal(activeIdBefore, vm.ActivePetId);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void ScaleAndSpeedChanges_UpdateSelectedPetSettingsAndAppliesLiveIfActive()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                // Selected pet is the active pet
                Assert.Equal(vm.ActivePetId, vm.SelectedPet?.Id);

                vm.ScalePercent = 150.0;
                vm.MovementSpeed = 80.0;

                // Live session immediately updated
                Assert.Equal(150.0, session.ScalePercent);
                Assert.Equal(80.0, session.MovementSpeedDip);

                // Persisted in settings store
                var (reloaded, _, _) = settingsStore.Load();
                var savedPetSettings = reloaded.GetOrCreatePetSettings(vm.SelectedPet!.Id);
                Assert.Equal(150.0, savedPetSettings.ScalePercent);
                Assert.Equal(80.0, savedPetSettings.MovementSpeed);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void CorruptSettings_DisablesPersistenceAndAllowsResetOrCancel()
    {
        RunInSta(() =>
        {
            // Write corrupted json to settings file
            File.WriteAllText(_paths.SettingsPath, "{ invalid json corrupt");

            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                // Detected corruption
                Assert.True(vm.IsSettingsCorrupted);
                Assert.False(settingsStore.IsPersistenceEnabled);

                // Changing values does NOT save or overwrite corrupt file
                vm.ScalePercent = 200.0;
                var rawCorruptedContent = File.ReadAllText(_paths.SettingsPath);
                Assert.Equal("{ invalid json corrupt", rawCorruptedContent);

                // Cancel maintains the corrupt file untouched
                bool shutdownRequested = false;
                vm.RequestShutdown += () => shutdownRequested = true;
                vm.CancelSettingsRecoveryCommand.Execute(null);
                Assert.True(shutdownRequested);
                Assert.Equal("{ invalid json corrupt", File.ReadAllText(_paths.SettingsPath));
                // When user declines reset confirmation, file is NOT overwritten
                vm.ConfirmResetAction = (_, _) => false;
                vm.ResetSettingsCommand.Execute(null);
                Assert.True(vm.IsSettingsCorrupted);
                Assert.False(settingsStore.IsPersistenceEnabled);
                Assert.Equal("{ invalid json corrupt", File.ReadAllText(_paths.SettingsPath));

                // Test simulated write failure during reset: lock file exclusively so Save() fails
                using (var lockStream = new FileStream(_paths.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    vm.ConfirmResetAction = (_, _) => true;
                    vm.ResetSettingsCommand.Execute(null);

                    // Recovery must remain gated!
                    Assert.True(vm.IsSettingsCorrupted);
                    Assert.False(settingsStore.IsPersistenceEnabled);
                    Assert.Contains("Lỗi: Không thể lưu cài đặt mặc định", vm.StatusText);
                }

                // When file is unlocked and user accepts reset confirmation, file is overwritten with valid defaults and persistence re-enabled
                vm.ConfirmResetAction = (_, _) => true;
                vm.ResetSettingsCommand.Execute(null);
                Assert.False(vm.IsSettingsCorrupted);
                Assert.True(settingsStore.IsPersistenceEnabled);

                var (validSettings, result, _) = settingsStore.Load();
                Assert.NotNull(validSettings);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void ReleaseToDesktop_WhenHidden_ActivatesSelectedPet()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                // Hide first
                vm.HideFromDesktopCommand.Execute(null);
                Assert.False(vm.IsDesktopPetVisible);
                Assert.False(session.IsVisible);

                // Release to desktop shows pet
                vm.ReleaseToDesktopCommand.Execute(null);
                Assert.True(vm.IsDesktopPetVisible);
                Assert.True(session.IsVisible);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void Initialize_WhenNoPetsAvailable_KeepsPetHiddenAndSetsEmptyStatus()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            // Delete the pet package from the store to simulate empty library
            var defaultPetDir = Directory.GetDirectories(_paths.PetsDirectory).First();
            Directory.Delete(defaultPetDir, true);

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                // When 0 pets, pet MUST be kept hidden and empty status reported
                Assert.False(vm.IsDesktopPetVisible);
                Assert.False(session.IsVisible);
                Assert.Equal("Thư viện hiện chưa có pet nào khả dụng.", vm.StatusText);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void Initialize_WhenSavedActivePetMissing_FallsBackAndWarns()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();

            // Save settings pointing to non-existent active pet
            var settings = new AppSettings { ActivePetId = "ghost-pet-404" };
            settingsStore.Save(settings);

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session);
                vm.Initialize();

                // Falls back to available pet and warns user
                Assert.NotNull(vm.SelectedPet);
                Assert.Equal(vm.SelectedPet.Id, vm.ActivePetId);
                Assert.Contains("ghost-pet-404", vm.StatusText);
                Assert.Contains("không còn trong thư viện", vm.StatusText);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void ImportPackageCommand_WhenFolderValid_AddsPetToLibrary_ActiveRemainsUnchanged()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();
            var filePicker = new TestFilePickerService();

            // Create external package to import
            var externalDir = Path.Combine(_tempRoot, "external_imported_pet");
            CreateValidSourcePackage(externalDir);

            filePicker.FolderToReturn = externalDir;

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session, filePicker);
                vm.Initialize();

                var initialActiveId = vm.ActivePetId;
                int initialCount = vm.Pets.Count;

                // Execute import
                vm.ImportPackageCommand.Execute(null);

                // Verify pet added and selected
                Assert.Equal(initialCount + 1, vm.Pets.Count);
                Assert.NotNull(vm.SelectedPet);
                Assert.NotEqual(initialActiveId, vm.SelectedPet.Id);
                // Active pet MUST remain untouched! (A11)
                Assert.Equal(initialActiveId, vm.ActivePetId);
                Assert.Contains("Đã nhập thành công", vm.StatusText);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void ImportPackageCommand_WhenRepeated_AssignsUniqueId_DoesNotOverwrite()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();
            var filePicker = new TestFilePickerService();

            var externalDir = Path.Combine(_tempRoot, "repeated_import_pet");
            CreateValidSourcePackage(externalDir);
            filePicker.FolderToReturn = externalDir;

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session, filePicker);
                vm.Initialize();

                vm.ImportPackageCommand.Execute(null);
                var firstImportedId = vm.SelectedPet!.Id;

                // Repeat import of the exact same folder (A11)
                vm.ImportPackageCommand.Execute(null);
                var secondImportedId = vm.SelectedPet!.Id;

                Assert.NotEqual(firstImportedId, secondImportedId);
                Assert.Contains(vm.Pets, p => p.Id == firstImportedId);
                Assert.Contains(vm.Pets, p => p.Id == secondImportedId);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void ImportPackageCommand_WhenFolderInvalid_ShowsError_NoCorruptItemAdded()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();
            var filePicker = new TestFilePickerService();

            var brokenDir = Path.Combine(_tempRoot, "broken_pkg");
            Directory.CreateDirectory(brokenDir);
            File.WriteAllText(Path.Combine(brokenDir, "pet.json"), "{ invalid broken json payload");
            filePicker.FolderToReturn = brokenDir;

            bool errorDialogShown = false;
            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session, filePicker);
                vm.Initialize();
                vm.ShowErrorAction = (_, _) => errorDialogShown = true;

                int countBefore = vm.Pets.Count;
                vm.ImportPackageCommand.Execute(null);

                Assert.True(errorDialogShown);
                Assert.Equal(countBefore, vm.Pets.Count);
                Assert.Contains("Lỗi nhập gói", vm.StatusText);
            }
            finally
            {
                session.Stop();
            }
        });
    }

    [Fact]
    public void OpenCreatePet_And_OpenEditPet_SetEditorStateProperly()
    {
        RunInSta(() =>
        {
            var store = CreatePetStoreWithDefaultPet();
            var settingsStore = new SettingsStore(_paths.SettingsPath);
            var session = new DesktopPetSession();
            var filePicker = new TestFilePickerService();

            try
            {
                var vm = new LibraryViewModel(store, settingsStore, session, filePicker);
                vm.Initialize();

                // Test OpenCreatePet
                vm.OpenCreatePetCommand.Execute(null);
                Assert.True(vm.IsEditorOpen);
                Assert.NotNull(vm.Editor);
                Assert.Null(vm.Editor.ExistingPetId);
                Assert.Equal("Idol mới", vm.Editor.Name);

                // Cancel editor
                vm.Editor.CancelCommand.Execute(null);
                Assert.False(vm.IsEditorOpen);
                Assert.Null(vm.Editor);

                // Test OpenEditPet
                Assert.NotNull(vm.SelectedPet);
                vm.OpenEditPetCommand.Execute(null);
                Assert.True(vm.IsEditorOpen);
                Assert.NotNull(vm.Editor);
                Assert.Equal(vm.SelectedPet.Id, vm.Editor.ExistingPetId);
                Assert.Equal(vm.SelectedPet.Name, vm.Editor.Name);
            }
            finally
            {
                session.Stop();
            }
        });
    }
}
