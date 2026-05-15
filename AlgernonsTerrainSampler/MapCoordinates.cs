using System.Runtime.CompilerServices;

namespace AlgernonsTerrainSampler;

/// <summary>
/// Float-precision 2D map coordinate on the X/Z plane.
/// </summary>
public readonly record struct MapCoordinateFloat(float X, float Z)
{
    public override string ToString() => $"({this.X}, {this.Z})";
}

/// <summary>
/// Block column world coordinate (X/Z in blocks).
/// </summary>
public readonly record struct WorldMapCoordinate(int X, int Z)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallChunkMapCoordinate ToChunk()
        => new(this.X / SmallChunkMapCoordinate.CHUNK_SIZE, this.Z / SmallChunkMapCoordinate.CHUNK_SIZE);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockInChunkMapCoordinate ToBlockInChunk()
        => new(this.X % SmallChunkMapCoordinate.CHUNK_SIZE, this.Z % SmallChunkMapCoordinate.CHUNK_SIZE);

    public override string ToString() => $"World({this.X}, {this.Z})";
}

/// <summary>
/// Small-chunk coordinate (each chunk is 32x32 blocks).
/// I call 32x32 chunks small chunks, and 64x64 large chunks.
/// Basegame only uses small chunks, and at the time of writing afaik only the watersheds mod uses large chunks.
/// </summary>
public readonly record struct SmallChunkMapCoordinate(int X, int Z)
{
    public const int CHUNK_SIZE = 32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RegionMapCoordinate ToRegion(int regionChunkSize)
        => new(this.X / regionChunkSize, this.Z / regionChunkSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkInRegionMapCoordinate ToChunkInRegion(int regionChunkSize)
        => new(this.X % regionChunkSize, this.Z % regionChunkSize);

    public override string ToString() => $"SmallChunk({this.X}, {this.Z})";
}

/// <summary>
/// Region coordinate.
/// </summary>
public readonly record struct RegionMapCoordinate(int X, int Z)
{
    public override string ToString() => $"Region({this.X}, {this.Z})";
}

/// <summary>
/// Chunk position within its region.
/// </summary>
public readonly record struct ChunkInRegionMapCoordinate(int X, int Z)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MapCoordinateFloat ScaleTo(float pixelSize) => new(this.X * pixelSize, this.Z * pixelSize);

    public override string ToString() => $"ChunkInRegion({this.X}, {this.Z})";
}

/// <summary>
/// Block position within its chunk.
/// </summary>
public readonly record struct BlockInChunkMapCoordinate(int X, int Z)
{
    public override string ToString() => $"BlockInChunk({this.X}, {this.Z})";
}
