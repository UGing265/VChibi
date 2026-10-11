using VChibi.Platform.Windows;

namespace VChibi.Runtime;

public class PetMovement
{
    public double CurrentX { get; set; }
    public double CurrentY { get; set; }

    public double TargetX { get; set; }
    public double TargetY { get; set; }

    public int MinX { get; private set; }
    public int MaxX { get; private set; }
    public int MinY { get; private set; }
    public int MaxY { get; private set; }

    public bool FacingLeft { get; private set; }

    public void UpdateBounds(WorkAreaInfo workArea, int widthPx, int heightPx)
    {
        MinX = workArea.Left;
        MaxX = Math.Max(MinX, workArea.Right - widthPx);

        MinY = workArea.Top;
        MaxY = Math.Max(MinY, workArea.Bottom - heightPx);

        // Clamp current position
        CurrentX = Math.Clamp(CurrentX, MinX, MaxX);
        CurrentY = Math.Clamp(CurrentY, MinY, MaxY);

        // Clamp target destination
        TargetX = Math.Clamp(TargetX, MinX, MaxX);
        TargetY = Math.Clamp(TargetY, MinY, MaxY);
    }

    public void SetDestination(double targetX, double targetY)
    {
        TargetX = Math.Clamp(targetX, MinX, MaxX);
        TargetY = Math.Clamp(targetY, MinY, MaxY);

        if (Math.Abs(TargetX - CurrentX) > 1.0)
        {
            FacingLeft = TargetX < CurrentX;
        }
    }

    public bool StepTowardsTarget(double speedPxPerSec, double deltaSeconds)
    {
        double dx = TargetX - CurrentX;
        double dy = TargetY - CurrentY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        double maxStep = speedPxPerSec * deltaSeconds;

        if (distance <= maxStep || distance < 0.5)
        {
            CurrentX = TargetX;
            CurrentY = TargetY;
            return true; // Arrived
        }

        double nx = dx / distance;
        double ny = dy / distance;

        CurrentX += nx * maxStep;
        CurrentY += ny * maxStep;

        if (Math.Abs(dx) > 1.0)
        {
            FacingLeft = dx < 0;
        }

        return false;
    }
}
