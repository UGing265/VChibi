using VChibi.Platform.Windows;
using VChibi.Runtime;
using Xunit;

namespace VChibi.Tests.Runtime;

public class PetControllerTests
{
    [Fact]
    public void PetController_TransitionsFromIdleToWalk_WhenIdleDurationElapsed()
    {
        var random = new Random(42);
        var controller = new PetController(random);
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 50, 50);

        Assert.Equal(PetState.Idle, controller.State);
        Assert.Equal("idle", controller.ActionName);

        // Max idle duration is 4.0 seconds. Advance by 4.5s in small increments
        for (int i = 0; i < 90; i++)
        {
            controller.Update(speedPxPerSec: 100.0, deltaSeconds: 0.05);
            if (controller.State == PetState.Walk) break;
        }

        Assert.Equal(PetState.Walk, controller.State);
        Assert.Equal("walk", controller.ActionName);
    }

    [Fact]
    public void PetController_TransitionsFromWalkToIdle_UponArrivalAtDestination()
    {
        var controller = new PetController();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 50, 50);

        // Set start and target close to each other
        controller.Movement.CurrentX = 100;
        controller.Movement.CurrentY = 100;
        controller.Movement.SetDestination(105, 100);

        // Force to Walk
        typeof(PetController).GetProperty("State")?.SetValue(controller, PetState.Walk);
        Assert.Equal(PetState.Walk, controller.State);

        // Advance with high speed so it arrives in 1 step
        controller.Update(speedPxPerSec: 200.0, deltaSeconds: 0.1);

        Assert.Equal(PetState.Idle, controller.State);
        Assert.Equal("idle", controller.ActionName);
    }

    [Fact]
    public void PetController_MultipleCycles_UndergoesRepeatedIdleAndWalkTransitions()
    {
        var random = new Random(12345);
        var controller = new PetController(random);
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 800, Bottom = 600, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 50, 50);

        int idleCount = 0;
        int walkCount = 0;
        int transitionCount = 0;
        PetState lastState = controller.State;

        // Simulate 40 seconds of elapsed time at 30 Hz (0.033s per step)
        for (int step = 0; step < 1200; step++)
        {
            controller.Update(speedPxPerSec: 150.0, deltaSeconds: 0.033);

            if (controller.State != lastState)
            {
                transitionCount++;
                lastState = controller.State;
            }

            if (controller.State == PetState.Idle) idleCount++;
            else if (controller.State == PetState.Walk) walkCount++;
        }

        // Must experience multiple cycles
        Assert.True(transitionCount >= 4, $"Expected at least 4 transitions between idle and walk, got {transitionCount}");
        Assert.True(idleCount > 0, "Expected non-zero idle time");
        Assert.True(walkCount > 0, "Expected non-zero walk time");
    }

    [Fact]
    public void PetController_FacingLeft_ReflectsHorizontalMovementDirection()
    {
        var controller = new PetController();
        var workArea = new WorkAreaInfo { Left = 0, Top = 0, Right = 1000, Bottom = 1000, Dpi = 96 };
        controller.Movement.UpdateBounds(workArea, 50, 50);

        controller.Movement.CurrentX = 500;
        controller.Movement.CurrentY = 500;

        // Move to the left (target X < current X)
        controller.Movement.SetDestination(200, 500);
        Assert.True(controller.FacingLeft);

        // Move to the right (target X > current X)
        controller.Movement.SetDestination(800, 500);
        Assert.False(controller.FacingLeft);
    }
}
