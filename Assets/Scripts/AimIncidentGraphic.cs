using UnityEngine;
using UnityEngine.UI;
using static AimIncidentData;

// Schematic sample view, never reconstructed map geometry or a player's camera.
public sealed class AimIncidentGraphic : Graphic
{
    private Incident _data;
    private int _frame = -1;
    private bool _side;
    private Vector2 _centre;
    private float _scale;
    public void SetEvidence(Incident data, bool side) { _data = data; _side = side; raycastTarget = false; Fit(); SetVerticesDirty(); }
    public void SetFrame(int frame) { _frame = frame; SetVerticesDirty(); }
    private Vector2 Plane(Point p) => new Vector2(p.x, _side ? p.y : p.z);
    private static Point RayEnd(Frame f)
    {
        var delta = new Vector3(f.target.x - f.eye.x, f.target.y - f.eye.y, f.target.z - f.eye.z);
        float length = Mathf.Clamp(delta.magnitude, 5, 100);
        return new Point(f.eye.x + f.aim.x * length, f.eye.y + f.aim.y * length, f.eye.z + f.aim.z * length);
    }
    private void Fit()
    {
        if (_data == null) return;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        void Include(Point point) { min = Vector2.Min(min, Plane(point)); max = Vector2.Max(max, Plane(point)); }
        foreach (var f in _data.frames)
        {
            if (f.shooterKnown) Include(f.shooter); if (f.targetKnown) Include(f.target);
            if (f.aimKnown && f.shooterKnown) { Include(f.eye); Include(RayEnd(f)); }
        }
        if (float.IsInfinity(min.x)) { min = Vector2.zero; max = Vector2.one; }
        _centre = (min + max) * .5f; Vector2 range = max - min;
        _scale = Mathf.Min((rectTransform.rect.width - 30) / Mathf.Max(1, range.x), (rectTransform.rect.height - 30) / Mathf.Max(1, range.y));
    }
    private Vector2 Project(Point p) => (Plane(p) - _centre) * _scale;
    private static void Quad(VertexHelper mesh, Vector2 centre, float size, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(centre + new Vector2(-size, -size), tint, Vector2.zero); mesh.AddVert(centre + new Vector2(size, -size), tint, Vector2.zero);
        mesh.AddVert(centre + new Vector2(size, size), tint, Vector2.zero); mesh.AddVert(centre + new Vector2(-size, size), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); if (_data == null) return; Fit();
        // Dots are actual samples. No path interpolation across samples or gaps.
        foreach (var f in _data.frames)
        {
            if (f.shooterKnown) Quad(mesh, Project(f.shooter), 1.5f, new Color(0, .7f, .8f, .45f));
            if (f.targetKnown) Quad(mesh, Project(f.target), 1.5f, new Color(1, .4f, .2f, .45f));
        }
        if (_frame < 0 || _frame >= _data.frames.Length) return;
        var frame = _data.frames[_frame];
        if (frame.shooterKnown) Quad(mesh, Project(frame.shooter), 4, Color.cyan);
        if (frame.targetKnown) Quad(mesh, Project(frame.target), 4, new Color(1, .45f, .15f));
        if (!frame.shooterKnown || !frame.aimKnown) return;
        Vector2 a = Project(frame.eye), b = Project(RayEnd(frame)), normal = new Vector2(a.y - b.y, b.x - a.x).normalized * 1.2f;
        int first = mesh.currentVertCount;
        foreach (Vector2 p in new[] { a - normal, a + normal, b + normal, b - normal }) mesh.AddVert(p, Color.yellow, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }
}
