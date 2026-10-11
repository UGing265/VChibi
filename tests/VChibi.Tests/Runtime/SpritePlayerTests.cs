using System.IO;
using VChibi.Models;
using VChibi.Runtime;
using VChibi.Services;
using Xunit;

namespace VChibi.Tests.Runtime;

public class SpritePlayerTests
{
    private readonly string _defaultPetDir;
    private readonly PetDefinition _defaultDefinition;

    public SpritePlayerTests()
    {
        _defaultPetDir = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPet");
        var manifestPath = Path.Combine(_defaultPetDir, "pet.json");
        PetValidator.ValidateManifestJson(manifestPath, out var def);
        _defaultDefinition = def!;
    }

    [Fact]
    public void LoadFromDefinition_LoadsDefaultPetSuccessfully()
    {
        var player = SpritePlayer.LoadFromDefinition(_defaultDefinition, _defaultPetDir);

        Assert.True(player.IsLoaded);
        Assert.True(player.MaxPixelWidth > 0);
        Assert.True(player.MaxPixelHeight > 0);
    }

    [Fact]
    public void GetFrame_CalculatesFrameIndexByElapsedTimeAndFps()
    {
        var player = SpritePlayer.LoadFromDefinition(_defaultDefinition, _defaultPetDir);

        // idle has 4 frames, fps = 6. Each frame is 1/6 s = ~0.1667 s.
        // t = 0.0s -> index 0
        // t = 0.20s -> index 1
        // t = 0.35s -> index 2
        // t = 0.55s -> index 3
        // t = 0.70s -> index 0 (loops around, cycle = 4/6 s = 0.6667s)

        player.GetFrame("idle", 0.0, out int idx0);
        Assert.Equal(0, idx0);

        player.GetFrame("idle", 0.20, out int idx1);
        Assert.Equal(1, idx1);

        player.GetFrame("idle", 0.35, out int idx2);
        Assert.Equal(2, idx2);

        player.GetFrame("idle", 0.55, out int idx3);
        Assert.Equal(3, idx3);

        player.GetFrame("idle", 0.70, out int idxLooped);
        Assert.Equal(0, idxLooped);
    }

    [Fact]
    public void GetFrame_WithUnknownAction_FallsBackToIdle()
    {
        var player = SpritePlayer.LoadFromDefinition(_defaultDefinition, _defaultPetDir);

        var frame = player.GetFrame("non_existent_action", 0.1, out int frameIndex);

        Assert.NotNull(frame);
        Assert.True(frameIndex >= 0);
    }
}
