namespace VChibi.Runtime;

public enum PetState
{
    Idle,
    Walk
}

public class PetController
{
    private readonly Random _random;
    private double _idleDuration = 2.0;
    private double _idleElapsed = 0.0;

    public PetMovement Movement { get; } = new();
    public PetState State { get; private set; } = PetState.Idle;

    public string ActionName => State == PetState.Idle ? "idle" : "walk";
    public bool FacingLeft => Movement.FacingLeft;

    public double CurrentX => Movement.CurrentX;
    public double CurrentY => Movement.CurrentY;

    public PetController(Random? random = null)
    {
        _random = random ?? new Random();
        PickNewIdleDuration();
    }

    private void PickNewIdleDuration()
    {
        // Idle between 1.0 and 4.0 seconds as specified in requirements
        _idleDuration = 1.0 + _random.NextDouble() * 3.0;
        _idleElapsed = 0.0;
    }

    public void PickNewDestination()
    {
        int minX = Movement.MinX;
        int maxX = Movement.MaxX;
        int minY = Movement.MinY;
        int maxY = Movement.MaxY;

        double targetX = minX < maxX ? minX + _random.NextDouble() * (maxX - minX) : minX;
        double targetY = minY < maxY ? minY + _random.NextDouble() * (maxY - minY) : minY;

        Movement.SetDestination(targetX, targetY);
        State = PetState.Walk;
    }

    public void Update(double speedPxPerSec, double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;

        // Clamp deltaSeconds to prevent teleportation after sleep/freeze (Review fix A3)
        deltaSeconds = Math.Min(deltaSeconds, 0.2);

        if (State == PetState.Idle)
        {
            _idleElapsed += deltaSeconds;
            if (_idleElapsed >= _idleDuration)
            {
                PickNewDestination();
            }
        }
        else if (State == PetState.Walk)
        {
            bool arrived = Movement.StepTowardsTarget(speedPxPerSec, deltaSeconds);
            if (arrived)
            {
                State = PetState.Idle;
                PickNewIdleDuration();
            }
        }
    }
}
