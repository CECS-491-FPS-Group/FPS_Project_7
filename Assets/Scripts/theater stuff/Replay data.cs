using System.Collections.Generic;

[System.Serializable]
public class ReplayData
{
    public string matchName;
    public string mapName;
    public float matchLength;

    public List<ReplayFrame> frames = new List<ReplayFrame>();
}