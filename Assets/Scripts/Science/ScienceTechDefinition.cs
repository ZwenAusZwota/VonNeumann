using System;

[Serializable]
public class ScienceTechDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public string Branch;
    public int Tier;
    public float DurationSeconds;
    public bool StartsUnlocked;
    public string[] Prerequisites = Array.Empty<string>();
}

public enum ScienceTechState
{
    Locked,
    Available,
    InProgress,
    Researched
}
