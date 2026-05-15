using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace AlgernonsTerrainSampler.Terrain;

public struct TerrainOctaves
{
    public double[] OctaveNoiseX0, OctaveNoiseX1, OctaveNoiseX2, OctaveNoiseX3;
    public double[] OctaveThresholdX0, OctaveThresholdX1, OctaveThresholdX2, OctaveThresholdX3;
    public TerrainOctaves(
        LerpedWeightedIndex2DMap landLerpMap,
        float chunkPixelSize,
        MapCoordinateFloat baseRegionChunk,
        int numberOfOctaves,
        LandformsWorldProperty landforms)
    {
        (this.OctaveNoiseX0, this.OctaveThresholdX0) = GetInterpolatedOctaves(
            landLerpMap[baseRegionChunk.X, baseRegionChunk.Z],
            numberOfOctaves,
            landforms);
        (this.OctaveNoiseX1, this.OctaveThresholdX1) = GetInterpolatedOctaves(
            landLerpMap[baseRegionChunk.X + chunkPixelSize, baseRegionChunk.Z],
            numberOfOctaves,
            landforms);
        (this.OctaveNoiseX2, this.OctaveThresholdX2) = GetInterpolatedOctaves(
            landLerpMap[baseRegionChunk.X, baseRegionChunk.Z + chunkPixelSize],
            numberOfOctaves,
            landforms);
        (this.OctaveNoiseX3, this.OctaveThresholdX3) = GetInterpolatedOctaves(
            landLerpMap[baseRegionChunk.X + chunkPixelSize, baseRegionChunk.Z + chunkPixelSize],
            numberOfOctaves,
            landforms);
    }

    private static (double[], double[]) GetInterpolatedOctaves(
        WeightedIndex[] octaveWeightIndices, int terrainGenOctaves, LandformsWorldProperty worldLandforms)
    {
        double[] amplitudes = new double[terrainGenOctaves];
        double[] thresholds = new double[terrainGenOctaves];

        for (int octave = 0; octave < terrainGenOctaves; octave++)
        {
            double amplitude = 0;
            double threshold = 0;
            for (int i = 0; i < octaveWeightIndices.Length; i++)
            {
                LandformVariant octaveLandform = worldLandforms.LandFormsByIndex[octaveWeightIndices[i].Index];
                amplitude += octaveLandform.TerrainOctaves[octave] * octaveWeightIndices[i].Weight;
                threshold += octaveLandform.TerrainOctaveThresholds[octave] * octaveWeightIndices[i].Weight;
            }

            amplitudes[octave] = amplitude;
            thresholds[octave] = threshold;
        }

        return (amplitudes, thresholds);
    }
}
