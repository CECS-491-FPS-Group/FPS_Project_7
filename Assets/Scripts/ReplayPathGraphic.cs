using UnityEngine;
using UnityEngine.UI;

public sealed class ReplayPathGraphic : Graphic
{
    private LocalReplayCapture.Recording _data;
    private Vector2 _min, _range;
    public void SetRecording(LocalReplayCapture.Recording data)
    {
        _data = data; _min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var frame in data.frames)
        { var point = new Vector2(frame.position.x, frame.position.z); _min = Vector2.Min(_min, point); max = Vector2.Max(max, point); }
        _range = max - _min; _range.x = Mathf.Max(1, _range.x); _range.y = Mathf.Max(1, _range.y);
        raycastTarget = false; SetVerticesDirty();
    }
    public Vector2 Project(Vector3 point)
    {
        Vector2 size = rectTransform.rect.size - Vector2.one * 20;
        return new Vector2((point.x - _min.x) / _range.x * size.x, (point.z - _min.y) / _range.y * size.y) - size * .5f;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); if (_data == null) return;
        int stride = Mathf.Max(1, Mathf.CeilToInt(_data.frames.Count / 2048f));
        for (int i = 0; i < _data.frames.Count - 1; i += stride)
        {
            Vector2 a = Project(_data.frames[i].position), b = Project(_data.frames[Mathf.Min(i + stride, _data.frames.Count - 1)].position);
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * 1.5f;
            int start = mesh.currentVertCount;
            foreach (var p in new[] { a - normal, a + normal, b + normal, b - normal }) mesh.AddVert(p, color, Vector2.zero);
            mesh.AddTriangle(start, start+1, start+2); mesh.AddTriangle(start, start+2, start+3);
        }
    }
}
