namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>Vertex of a surface mesh, in millimeters.</summary>
public readonly record struct MeshVertex(double X, double Y, double Z);

/// <summary>
/// Triangle of a closed surface mesh. Vertices run counterclockwise seen from outside, so the right-hand rule
/// gives the outward normal (the STL convention).
/// </summary>
public readonly record struct MeshTriangle(MeshVertex A, MeshVertex B, MeshVertex C);
