using VChibi.Platform.Windows;
using VChibi.Runtime;
using Xunit;

namespace VChibi.Tests.Runtime;

public class PetMovementTests
{
    [Fact]
    public void UpdateBounds_WithStandardScreen_ClampsPositionsWithinBounds()
    {
        var movement = new PetMovement();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1920, Bottom = 1080, Dpi = 96 };

        // Pet size: 100x100 px
        movement.UpdateBounds(workArea, 100, 100);

        Assert.Equal(0, movement.MinX);
        Assert.Equal(1820, movement.MaxX);
        Assert.Equal(0, movement.MinY);
        Assert.Equal(980, movement.MaxY);
    }

    [Fact]
    public void UpdateBounds_WithNonZeroOrigin_ClampsCorrectly()
    {
        var movement = new PetMovement();
        // Shifted origin (e.g. secondary display or taskbar on left/top)
        var workArea = new WorkAreaInfo { Left = 100, Top = 50, Right = 1920, Bottom = 1080, Dpi = 120 };

        movement.UpdateBounds(workArea, 120, 120);

        Assert.Equal(100, movement.MinX);
        Assert.Equal(1920 - 120, movement.MaxX);
        Assert.Equal(50, movement.MinY);
        Assert.Equal(1080 - 120, movement.MaxY);

        // Current coordinates must be clamped inside bounds
        Assert.True(movement.CurrentX >= movement.MinX);
        Assert.True(movement.CurrentX <= movement.MaxX);
        Assert.True(movement.CurrentY >= movement.MinY);
        Assert.True(movement.CurrentY <= movement.MaxY);
    }

    [Fact]
    public void StepTowardsTarget_WhenAlreadyAtTarget_ReturnsArrivedWithoutDivisionByZero()
    {
        var movement = new PetMovement();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        movement.UpdateBounds(workArea, 50, 50);

        movement.CurrentX = 200;
        movement.CurrentY = 200;
        movement.TargetX = 200;
        movement.TargetY = 200;

        bool arrived = movement.StepTowardsTarget(speedPxPerSec: 100.0, deltaSeconds: 0.1);

        Assert.True(arrived);
        Assert.False(double.IsNaN(movement.CurrentX));
        Assert.False(double.IsNaN(movement.CurrentY));
        Assert.Equal(200, movement.CurrentX);
        Assert.Equal(200, movement.CurrentY);
    }

    [Fact]
    public void StepTowardsTarget_AdvancesCorrectDistanceAlongVector()
    {
        var movement = new PetMovement();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        movement.UpdateBounds(workArea, 50, 50);

        movement.CurrentX = 0;
        movement.CurrentY = 0;
        movement.SetDestination(300, 400); // 3-4-5 triangle, total distance = 500

        // Step speed = 500 px/s, delta = 0.1s -> maxStep = 50 px
        bool arrived = movement.StepTowardsTarget(speedPxPerSec: 500.0, deltaSeconds: 0.1);

        Assert.False(arrived);
        Assert.Equal(30.0, movement.CurrentX, 3);
        Assert.Equal(40.0, movement.CurrentY, 3);
    }

    [Fact]
    public void StepTowardsTarget_WhenDistanceLessThanMaxStep_ArrivesExactly()
    {
        var movement = new PetMovement();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        movement.UpdateBounds(workArea, 50, 50);

        movement.CurrentX = 100;
        movement.CurrentY = 100;
        movement.SetDestination(103, 104); // distance = 5

        // Speed = 100 px/s, delta = 0.1s -> maxStep = 10 px
        bool arrived = movement.StepTowardsTarget(speedPxPerSec: 100.0, deltaSeconds: 0.1);

        Assert.True(arrived);
        Assert.Equal(103, movement.CurrentX);
        Assert.Equal(104, movement.CurrentY);
    }

    [Fact]
    public void PetController_PickNewDestination_AlwaysGeneratesCoordinatesInsideBounds()
    {
        var controller = new PetController();
        var workArea = new WorkAreaInfo { Left = 200, Top = 100, Right = 800, Bottom = 600, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 64, 64);

        for (int i = 0; i < 50; i++)
        {
            controller.PickNewDestination();
            Assert.True(controller.Movement.TargetX >= controller.Movement.MinX, $"TargetX {controller.Movement.TargetX} < {controller.Movement.MinX}");
            Assert.True(controller.Movement.TargetX <= controller.Movement.MaxX, $"TargetX {controller.Movement.TargetX} > {controller.Movement.MaxX}");
            Assert.True(controller.Movement.TargetY >= controller.Movement.MinY, $"TargetY {controller.Movement.TargetY} < {controller.Movement.MinY}");
            Assert.True(controller.Movement.TargetY <= controller.Movement.MaxY, $"TargetY {controller.Movement.TargetY} > {controller.Movement.MaxY}");
        }
    }

    [Fact]
    public void PetController_Update_WithLargeDelta_ClampsDeltaToPreventTeleportation()
    {
        var controller = new PetController();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1920, Bottom = 1080, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 50, 50);

        controller.Movement.CurrentX = 100;
        controller.Movement.CurrentY = 100;
        controller.Movement.SetDestination(1000, 1000); // 900 px away

        // Force state to Walk
        typeof(PetController).GetProperty("State")?.SetValue(controller, PetState.Walk);

        // Simulate 30-second system sleep / freeze
        double speedPxPerSec = 100.0;
        double hugeDelta = 30.0; // 30 seconds

        controller.Update(speedPxPerSec, hugeDelta);

        // Delta must be clamped to at most 0.2s!
        // Max distance traversed = 100 px/s * 0.2s = 20 px
        double dx = controller.CurrentX - 100;
        double dy = controller.CurrentY - 100;
        double distanceTraversed = Math.Sqrt(dx * dx + dy * dy);

        Assert.True(distanceTraversed <= 20.001, $"Traversed distance {distanceTraversed} exceeded max clamp of 20px");
    }

    [Fact]
    public void PetMovement_UpdateBounds_WhenBoundsShrinkMidWalk_ClampsPositionAndDestination()
    {
        var movement = new PetMovement();
        var largeWorkArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 2560, Bottom = 1440, Dpi = 96 };
        movement.UpdateBounds(largeWorkArea, 100, 100);

        movement.CurrentX = 2000;
        movement.CurrentY = 1200;
        movement.SetDestination(2200, 1300);

        // Shrink screen (e.g. resolution change, dock disconnect) to 1280x720
        var smallWorkArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1280, Bottom = 720, Dpi = 96 };
        movement.UpdateBounds(smallWorkArea, 100, 100);

        // Both Current and Target must now be clamped within 1280-100 = 1180, 720-100 = 620
        Assert.True(movement.CurrentX <= 1180, $"CurrentX {movement.CurrentX} > 1180");
        Assert.True(movement.CurrentY <= 620, $"CurrentY {movement.CurrentY} > 620");
        Assert.True(movement.TargetX <= 1180, $"TargetX {movement.TargetX} > 1180");
        Assert.True(movement.TargetY <= 620, $"TargetY {movement.TargetY} > 620");
    }
}
