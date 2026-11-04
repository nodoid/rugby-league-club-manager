using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RugbyLeague.Game.Graphics;

/// <summary>Builds simple flat-shaded meshes as coloured triangles for the 3D match.</summary>
public sealed class MeshBuilder
{
    private readonly List<VertexPositionColor> _vertices = new();

    public int VertexCount => _vertices.Count;

    public void Clear() => _vertices.Clear();

    public VertexPositionColor[] ToArray() => _vertices.ToArray();

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        _vertices.Add(new VertexPositionColor(a, color));
        _vertices.Add(new VertexPositionColor(b, color));
        _vertices.Add(new VertexPositionColor(c, color));
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        AddTriangle(a, b, c, color);
        AddTriangle(a, c, d, color);
    }

    public void AddBox(Vector3 center, Vector3 size, Color color)
    {
        var h = size / 2f;
        var min = center - h;
        var max = center + h;

        var p000 = new Vector3(min.X, min.Y, min.Z);
        var p100 = new Vector3(max.X, min.Y, min.Z);
        var p110 = new Vector3(max.X, min.Y, max.Z);
        var p010 = new Vector3(min.X, min.Y, max.Z);
        var p001 = new Vector3(min.X, max.Y, min.Z);
        var p101 = new Vector3(max.X, max.Y, min.Z);
        var p111 = new Vector3(max.X, max.Y, max.Z);
        var p011 = new Vector3(min.X, max.Y, max.Z);

        var top = color;
        var side = color * 0.82f;
        var sideDark = color * 0.7f;

        AddQuad(p001, p101, p111, p011, top);             // top
        AddQuad(p010, p110, p111, p011, Color.Lerp(color, Color.Black, 0.22f) * 0.9f); // +z
        AddQuad(p000, p100, p110, p010, sideDark);        // -z
        AddQuad(p100, p110, p111, p101, side);            // +x
        AddQuad(p000, p010, p011, p001, side);            // -x
    }

    public void AddFlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) =>
        AddQuad(a, b, c, d, color);

    public void AddDisc(Vector3 center, float radius, Color color, int segments = 24)
    {
        for (var i = 0; i < segments; i++)
        {
            var angleA = MathHelper.TwoPi * i / segments;
            var angleB = MathHelper.TwoPi * (i + 1) / segments;
            var a = center + new Vector3(MathF.Cos(angleA) * radius, 0, MathF.Sin(angleA) * radius);
            var b = center + new Vector3(MathF.Cos(angleB) * radius, 0, MathF.Sin(angleB) * radius);
            AddTriangle(center, a, b, color);
        }
    }

    public void AddRing(Vector3 center, float innerRadius, float outerRadius, Color color, int segments = 32)
    {
        for (var i = 0; i < segments; i++)
        {
            var angleA = MathHelper.TwoPi * i / segments;
            var angleB = MathHelper.TwoPi * (i + 1) / segments;

            var a = center + new Vector3(MathF.Cos(angleA) * outerRadius, 0, MathF.Sin(angleA) * outerRadius);
            var b = center + new Vector3(MathF.Cos(angleB) * outerRadius, 0, MathF.Sin(angleB) * outerRadius);
            var c = center + new Vector3(MathF.Cos(angleB) * innerRadius, 0, MathF.Sin(angleB) * innerRadius);
            var d = center + new Vector3(MathF.Cos(angleA) * innerRadius, 0, MathF.Sin(angleA) * innerRadius);

            AddQuad(a, b, c, d, color);
        }
    }

    public void AddCylinder(Vector3 baseCenter, float radius, float height, Color color, int segments = 12,
        bool cap = true)
    {
        var topCenter = baseCenter + new Vector3(0, height, 0);
        var side = color * 0.86f;

        for (var i = 0; i < segments; i++)
        {
            var angleA = MathHelper.TwoPi * i / segments;
            var angleB = MathHelper.TwoPi * (i + 1) / segments;

            var offsetA = new Vector3(MathF.Cos(angleA) * radius, 0, MathF.Sin(angleA) * radius);
            var offsetB = new Vector3(MathF.Cos(angleB) * radius, 0, MathF.Sin(angleB) * radius);

            AddQuad(baseCenter + offsetA, baseCenter + offsetB, topCenter + offsetB, topCenter + offsetA, side);

            if (!cap) continue;

            AddTriangle(topCenter, topCenter + offsetA, topCenter + offsetB, color);
            AddTriangle(baseCenter, baseCenter + offsetB, baseCenter + offsetA, color * 0.6f);
        }
    }

    public void AddSphere(Vector3 center, float radius, Color color, int segments = 10, int rings = 6)
    {
        for (var ring = 0; ring < rings; ring++)
        {
            var phiA = MathF.PI * ring / rings;
            var phiB = MathF.PI * (ring + 1) / rings;

            for (var segment = 0; segment < segments; segment++)
            {
                var thetaA = MathHelper.TwoPi * segment / segments;
                var thetaB = MathHelper.TwoPi * (segment + 1) / segments;

                Vector3 Point(float phi, float theta) => center + new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta) * radius,
                    MathF.Cos(phi) * radius,
                    MathF.Sin(phi) * MathF.Sin(theta) * radius);

                var a = Point(phiA, thetaA);
                var b = Point(phiA, thetaB);
                var c = Point(phiB, thetaB);
                var d = Point(phiB, thetaA);
                var shade = Color.Lerp(color, Color.White, 0.12f + 0.12f * MathF.Cos(phiA));
                AddQuad(a, b, c, d, shade);
            }
        }
    }

    /// <summary>
    /// A squashed sphere. Rugby balls are prolate spheroids, so the long axis is stretched and the
    /// other two axes pinched in.
    /// </summary>
    public void AddEllipsoid(Vector3 center, float length, float width, float height, Color color,
        int segments = 12, int rings = 8)
    {
        var scale = new Vector3(length * 0.5f, height * 0.5f, width * 0.5f);

        for (var ring = 0; ring < rings; ring++)
        {
            var phiA = MathF.PI * ring / rings;
            var phiB = MathF.PI * (ring + 1) / rings;

            for (var segment = 0; segment < segments; segment++)
            {
                var thetaA = MathHelper.TwoPi * segment / segments;
                var thetaB = MathHelper.TwoPi * (segment + 1) / segments;

                Vector3 Point(float phi, float theta) => center + new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta) * scale.X,
                    MathF.Cos(phi) * scale.Y,
                    MathF.Sin(phi) * MathF.Sin(theta) * scale.Z);

                var a = Point(phiA, thetaA);
                var b = Point(phiA, thetaB);
                var c = Point(phiB, thetaB);
                var d = Point(phiB, thetaA);
                var shade = Color.Lerp(color, Color.White, 0.1f + 0.14f * MathF.Cos(phiA));
                AddQuad(a, b, c, d, shade);
            }
        }
    }
}
