namespace GammaKey.App;

// Which profile is on screen right now. Null means the original (baseline) is showing.
public sealed class ActiveProfile
{
    public string? Id { get; private set; }

    public void Applied(string id) => Id = id;

    public void Cleared() => Id = null;

    public bool IsActive(string id) => Id == id;
}
