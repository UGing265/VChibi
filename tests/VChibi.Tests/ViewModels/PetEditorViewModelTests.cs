using System.IO;
using VChibi.Models;
using VChibi.Services;
using VChibi.ViewModels;
using Xunit;

namespace VChibi.Tests.ViewModels;

public class TestFilePickerService : IFilePickerService
{
    public IReadOnlyList<string>? PngFilesToReturn { get; set; }
    public string? FolderToReturn { get; set; }

    public IReadOnlyList<string>? PickPngFiles(string title = "Chọn hình ảnh PNG") => PngFilesToReturn;
    public string? PickFolder(string title = "Chọn thư mục gói pet") => FolderToReturn;
}

public class PetEditorViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppPaths _paths;
    private readonly AppLock _appLock;
    private readonly PetStore _petStore;
    private readonly TestFilePickerService _filePicker;

    public PetEditorViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vchibi_editor_test_{Guid.NewGuid():N}");
        _paths = new AppPaths(_tempDir);
        _paths.EnsureDirectoriesCreated();
        _appLock = new AppLock(_paths.LockFilePath);
        _appLock.TryAcquire();
        _petStore = new PetStore(_paths, _appLock);
        _filePicker = new TestFilePickerService();
    }

    public void Dispose()
    {
        _appLock.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string CreateValidPng(string path, int width = 16, int height = 16)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 120, 98, 184));
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    private PetDefinition CreateSamplePet(string id, string name)
    {
        var idle1 = CreateValidPng(Path.Combine(_tempDir, "sample", "idle1.png"));
        var walk1 = CreateValidPng(Path.Combine(_tempDir, "sample", "walk1.png"));

        var actions = new Dictionary<string, (int Fps, IReadOnlyList<string> SourceFramePaths)>
        {
            ["idle"] = (8, new[] { idle1 }),
            ["walk"] = (12, new[] { walk1 })
        };

        return _petStore.SavePetDraft(id, name, actions);
    }

    [Fact]
    public void DraftIsolation_ModifyingDraft_DoesNotMutateOriginalPetDefinition()
    {
        var original = CreateSamplePet("sample-pet", "Original Name");
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeFromPet(original);

        Assert.Equal("Original Name", vm.Name);
        Assert.Equal(8, vm.IdleFps);
        Assert.Equal(12, vm.WalkFps);
        Assert.False(vm.IsDirty);

        // Modify draft in editor
        vm.Name = "Modified Draft Name";
        vm.IdleFps = 24;

        Assert.True(vm.IsDirty);
        // Original definition remains untouched!
        Assert.Equal("Original Name", original.Name);
        Assert.Equal(8, original.Animations["idle"].Fps);
    }

    [Fact]
    public void Cancel_WhenNotDirty_CancelsWithoutPrompt()
    {
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();

        bool promptInvoked = false;
        vm.ConfirmCancelAction = (_, _) =>
        {
            promptInvoked = true;
            return false;
        };

        bool cancelledInvoked = false;
        vm.Cancelled += () => cancelledInvoked = true;

        vm.CancelCommand.Execute(null);

        Assert.False(promptInvoked);
        Assert.True(cancelledInvoked);
    }

    [Fact]
    public void Cancel_WhenDirty_PromptsAndCancelsOnlyIfConfirmed()
    {
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.Name = "Dirty Name";

        Assert.True(vm.IsDirty);

        // Case 1: User declines confirmation prompt
        bool promptInvoked = false;
        vm.ConfirmCancelAction = (_, _) =>
        {
            promptInvoked = true;
            return false; // Denied
        };

        bool cancelledInvoked = false;
        vm.Cancelled += () => cancelledInvoked = true;

        vm.CancelCommand.Execute(null);

        Assert.True(promptInvoked);
        Assert.False(cancelledInvoked);

        // Case 2: User confirms prompt
        vm.ConfirmCancelAction = (_, _) => true;
        vm.CancelCommand.Execute(null);

        Assert.True(cancelledInvoked);
    }

    [Fact]
    public void AddFrames_SortsNaturallyByFilename()
    {
        var f10 = CreateValidPng(Path.Combine(_tempDir, "sources", "frame10.png"));
        var f2 = CreateValidPng(Path.Combine(_tempDir, "sources", "frame2.png"));
        var f1 = CreateValidPng(Path.Combine(_tempDir, "sources", "frame1.png"));

        // File picker returns them in arbitrary order
        _filePicker.PngFilesToReturn = new[] { f10, f2, f1 };

        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();

        vm.AddFramesCommand.Execute(null);

        Assert.Equal(3, vm.CurrentFrames.Count);
        Assert.Equal("frame1.png", vm.CurrentFrames[0].FileName);
        Assert.Equal(1, vm.CurrentFrames[0].Index);
        Assert.Equal("frame2.png", vm.CurrentFrames[1].FileName);
        Assert.Equal(2, vm.CurrentFrames[1].Index);
        Assert.Equal("frame10.png", vm.CurrentFrames[2].FileName);
        Assert.Equal(3, vm.CurrentFrames[2].Index);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void FrameReordering_MoveUpAndDown_UpdatesIndicesAndMaintainsSelection()
    {
        var f1 = CreateValidPng(Path.Combine(_tempDir, "sources", "f1.png"));
        var f2 = CreateValidPng(Path.Combine(_tempDir, "sources", "f2.png"));
        var f3 = CreateValidPng(Path.Combine(_tempDir, "sources", "f3.png"));

        _filePicker.PngFilesToReturn = new[] { f1, f2, f3 };
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.AddFramesCommand.Execute(null);

        // Select second frame
        vm.SelectedFrame = vm.CurrentFrames[1];
        Assert.Equal("f2.png", vm.SelectedFrame.FileName);

        // Move up
        vm.MoveFrameUpCommand.Execute(null);
        Assert.Equal("f2.png", vm.CurrentFrames[0].FileName);
        Assert.Equal(1, vm.CurrentFrames[0].Index);
        Assert.Equal("f1.png", vm.CurrentFrames[1].FileName);
        Assert.Equal(2, vm.CurrentFrames[1].Index);
        Assert.Equal(vm.CurrentFrames[0], vm.SelectedFrame);

        // Move down
        vm.MoveFrameDownCommand.Execute(null);
        Assert.Equal("f1.png", vm.CurrentFrames[0].FileName);
        Assert.Equal("f2.png", vm.CurrentFrames[1].FileName);
        Assert.Equal(vm.CurrentFrames[1], vm.SelectedFrame);
    }

    [Fact]
    public void RemoveFrame_UpdatesIndicesAndSelection()
    {
        var f1 = CreateValidPng(Path.Combine(_tempDir, "sources", "f1.png"));
        var f2 = CreateValidPng(Path.Combine(_tempDir, "sources", "f2.png"));

        _filePicker.PngFilesToReturn = new[] { f1, f2 };
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.AddFramesCommand.Execute(null);

        vm.SelectedFrame = vm.CurrentFrames[0];
        vm.RemoveFrameCommand.Execute(null);

        Assert.Single(vm.CurrentFrames);
        Assert.Equal("f2.png", vm.CurrentFrames[0].FileName);
        Assert.Equal(1, vm.CurrentFrames[0].Index);
        Assert.Equal(vm.CurrentFrames[0], vm.SelectedFrame);
    }

    [Fact]
    public void Save_CommitsToStore_AndInvokesPetSavedWithFalse()
    {
        var fIdle = CreateValidPng(Path.Combine(_tempDir, "sources", "idle01.png"));
        var fWalk = CreateValidPng(Path.Combine(_tempDir, "sources", "walk01.png"));

        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.Name = "Saved Chibi";

        // Add idle
        vm.SelectAction("idle");
        _filePicker.PngFilesToReturn = new[] { fIdle };
        vm.AddFramesCommand.Execute(null);

        // Add walk
        vm.SelectAction("walk");
        _filePicker.PngFilesToReturn = new[] { fWalk };
        vm.AddFramesCommand.Execute(null);

        Assert.True(vm.CanSave);

        PetDefinition? savedResult = null;
        bool? activateResult = null;
        vm.PetSaved += (def, andActivate) =>
        {
            savedResult = def;
            activateResult = andActivate;
        };

        vm.SaveCommand.Execute(null);

        Assert.NotNull(savedResult);
        Assert.Equal("Saved Chibi", savedResult.Name);
        Assert.False(activateResult);
        Assert.False(vm.IsDirty);

        // Verify package exists on disk
        var inStore = _petStore.GetPet(savedResult.Id);
        Assert.NotNull(inStore);
        Assert.Equal("Saved Chibi", inStore.Name);
    }

    [Fact]
    public void SaveAndActivate_CommitsToStore_AndInvokesPetSavedWithTrue()
    {
        var fIdle = CreateValidPng(Path.Combine(_tempDir, "sources", "idle01.png"));
        var fWalk = CreateValidPng(Path.Combine(_tempDir, "sources", "walk01.png"));

        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.Name = "Active Chibi";

        vm.SelectAction("idle");
        _filePicker.PngFilesToReturn = new[] { fIdle };
        vm.AddFramesCommand.Execute(null);

        vm.SelectAction("walk");
        _filePicker.PngFilesToReturn = new[] { fWalk };
        vm.AddFramesCommand.Execute(null);

        PetDefinition? savedResult = null;
        bool? activateResult = null;
        vm.PetSaved += (def, andActivate) =>
        {
            savedResult = def;
            activateResult = andActivate;
        };

        vm.SaveAndActivateCommand.Execute(null);

        Assert.NotNull(savedResult);
        Assert.True(activateResult);
    }

    [Fact]
    public void CanSave_RequiresValidNameFramesAndFps()
    {
        var vm = new PetEditorViewModel(_petStore, _filePicker);
        vm.InitializeNew();
        vm.Name = "";
        Assert.False(vm.CanSave);
        Assert.NotEmpty(vm.ValidationSummary);

        vm.Name = "Test Pet";
        Assert.False(vm.CanSave); // Missing frames

        var f = CreateValidPng(Path.Combine(_tempDir, "sources", "f.png"));
        _filePicker.PngFilesToReturn = new[] { f };
        vm.SelectAction("idle");
        vm.AddFramesCommand.Execute(null);
        Assert.False(vm.CanSave); // Missing walk frames

        vm.SelectAction("walk");
        vm.AddFramesCommand.Execute(null);
        Assert.True(vm.CanSave);
        Assert.Empty(vm.ValidationSummary);
    }
}
