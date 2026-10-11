using System.IO;
using System.Reflection;
using System.Threading;
using VChibi.Models;
using VChibi.Runtime;
using VChibi.Services;
using Xunit;

namespace VChibi.Tests.Runtime;

public class DesktopPetSessionTests
{
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

    private static PetDefinition GetDefaultDefinition()
    {
        var defaultDir = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPet");
        var manifestPath = Path.Combine(defaultDir, "pet.json");
        PetValidator.ValidateManifestJson(manifestPath, out var def);
        def!.RootDirectory = defaultDir;
        return def;
    }

    [Fact]
    public void ActivatePet_LoadsDefaultPetAndInitializesDimensions()
    {
        RunInSta(() =>
        {
            var def = GetDefaultDefinition();
            var session = new DesktopPetSession();
            session.ActivatePet(def);

            Assert.True(session.Player.IsLoaded);
            Assert.True(session.Window.Width > 0);
            Assert.True(session.Window.Height > 0);

            session.Stop();
        });
    }

    [Fact]
    public void SetDisplaySettings_ClampsValuesWithinSafeLimits()
    {
        RunInSta(() =>
        {
            var session = new DesktopPetSession();

            // Set extreme values: scale 500% (max 400), speed 1000 (max 200)
            session.SetDisplaySettings(500.0, 1000.0);
            Assert.Equal(PetDisplaySettings.MaxScale, session.ScalePercent);
            Assert.Equal(PetDisplaySettings.MaxSpeed, session.MovementSpeedDip);

            // Set sub-minimum values: scale 5% (min 25), speed 1 (min 5)
            session.SetDisplaySettings(5.0, 1.0);
            Assert.Equal(PetDisplaySettings.MinScale, session.ScalePercent);
            Assert.Equal(PetDisplaySettings.MinSpeed, session.MovementSpeedDip);

            session.Stop();
        });
    }

    [Fact]
    public void ShowAndHide_TogglesIsVisibleState()
    {
        RunInSta(() =>
        {
            var def = GetDefaultDefinition();
            var session = new DesktopPetSession();
            session.ActivatePet(def);

            Assert.False(session.IsVisible);

            session.Show();
            Assert.True(session.IsVisible);

            session.Hide();
            Assert.False(session.IsVisible);

            session.Stop();
        });
    }

    [Fact]
    public void TryActivatePet_WhenNewLoadFails_PreservesOldPet()
    {
        RunInSta(() =>
        {
            var def = GetDefaultDefinition();
            var session = new DesktopPetSession();
            session.ActivatePet(def);
            Assert.True(session.Player.IsLoaded);

            // Create invalid definition with missing assets
            var badDef = new PetDefinition
            {
                SchemaVersion = 1,
                Id = "bad-pet",
                Name = "Bad Pet",
                RootDirectory = Path.Combine(AppContext.BaseDirectory, "NonExistentDirectory"),
                Animations = new Dictionary<string, AnimationDefinition>
                {
                    ["idle"] = new() { Fps = 8, Frames = new() { "idle/missing.png" } }
                }
            };

            var oldPlayer = session.Player;
            bool activated = session.TryActivatePet(badDef);
            Assert.False(activated);

            // Old player must still be loaded and be the exact same instance
            Assert.True(session.Player.IsLoaded);
            Assert.Same(oldPlayer, session.Player);
            session.Stop();
        });
    }

    [Fact]
    public void Stop_CleansUpWindowFrameAndResetsPlayer()
    {
        RunInSta(() =>
        {
            var def = GetDefaultDefinition();
            var session = new DesktopPetSession();
            session.ActivatePet(def);
            session.Show();

            session.Stop();
            Assert.False(session.IsVisible);
            Assert.False(session.Player.IsLoaded);
            Assert.Null(session.Window.SpriteImage.Source);
        });
    }

    [Fact]
    public void PowerModeChanged_SuspendAndResume_ControlsTimerProperly()
    {
        RunInSta(() =>
        {
            var def = GetDefaultDefinition();
            var session = new DesktopPetSession();
            session.ActivatePet(def);
            session.Show();
            Assert.True(session.IsTimerRunning);

            // Suspend: stops timer
            session.HandlePowerModeChangedForTest(Microsoft.Win32.PowerModes.Suspend);
            Assert.False(session.IsTimerRunning);

            // Resume while visible: restarts timer
            session.HandlePowerModeChangedForTest(Microsoft.Win32.PowerModes.Resume);
            Assert.True(session.IsTimerRunning);

            // Hide and Suspend/Resume: stays stopped when hidden
            session.Hide();
            session.HandlePowerModeChangedForTest(Microsoft.Win32.PowerModes.Suspend);
            Assert.False(session.IsTimerRunning);
            session.HandlePowerModeChangedForTest(Microsoft.Win32.PowerModes.Resume);
            Assert.False(session.IsTimerRunning);

            session.Stop();
        });
    }
}
