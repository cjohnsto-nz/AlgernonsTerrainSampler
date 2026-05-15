using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace AlgernonsTerrainSampler.Terrain;

public partial class TerrainSamplerNormalizedSimplexFractalNoise
{
    public TerrainSamplerNormalizedSimplexFractalNoise(double[] inputAmplitudes, double[] frequencies, long seed)
    {
        this.frequencies = frequencies;
        this.octaveSeeds = new long[inputAmplitudes.Length];
        for (int i = 0; i < this.octaveSeeds.Length; i++)
        {
            this.octaveSeeds[i] = (seed * 65599L) + i;
        }
    }

    public static TerrainSamplerNormalizedSimplexFractalNoise FromDefaultOctaves(int quantityOctaves, double baseFrequency, double persistence, long seed)
    {
        double[] frequencies = new double[quantityOctaves];
        double[] amplitudes = new double[quantityOctaves];
        for (int i = 0; i < quantityOctaves; i++)
        {
            frequencies[i] = Math.Pow(2.0, i) * baseFrequency;
            amplitudes[i] = Math.Pow(persistence, i);
        }

        return new TerrainSamplerNormalizedSimplexFractalNoise(amplitudes, frequencies, seed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NoiseValueCurve(double value)
        => (value / Math.Sqrt(1.0 + (value * value)) * 0.5) + 0.5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyThresholding(double value, double threshold, double smoothingFactor)
        => GameMath.SmoothMax(0.0, value - threshold, smoothingFactor) + GameMath.SmoothMin(0.0, value + threshold, smoothingFactor);

    public ColumnNoise ForColumn(double relativeYFrequency, double[] amplitudes, double[] thresholds, double noiseX, double noiseZ)
        => new(this, relativeYFrequency, amplitudes, thresholds, noiseX, noiseZ);

    public readonly double[] frequencies;
    public readonly long[] octaveSeeds;

    public partial struct ColumnNoise
    {
        // Mirror of NewNormalizedSimplexFractalNoise.ValueMultiplier
        private const double ValueMultiplier = 1.1845758506756423;

        private const int MaxOctavesOnStack = 64;
        [ThreadStatic] private static OctaveEntry[] threadLocalOctaveEntries;
        [ThreadStatic] private static PastEvaluation[] threadLocalPastEvaluations;

        public double BoundMin { readonly get; private set; }
        public double BoundMax { readonly get; private set; }

        public ColumnNoise(
            TerrainSamplerNormalizedSimplexFractalNoise terrainNoise,
            double relativeYFrequency,
            double[] amplitudes,
            double[] thresholds,
            double noiseX,
            double noiseZ)
        {
            int octaveCount = terrainNoise.frequencies.Length;
            int nUsedOctaves = 0;
            Span<double> maxValues = octaveCount <= MaxOctavesOnStack ? stackalloc double[octaveCount] : new double[octaveCount];
            Span<int> order = octaveCount <= MaxOctavesOnStack ? stackalloc int[octaveCount] : new int[octaveCount];
            double bound = 0.0;
            for (int i = octaveCount - 1; i >= 0; i--)
            {
                maxValues[i] = Math.Max(0.0, Math.Abs(amplitudes[i]) - thresholds[i]) * ValueMultiplier;
                bound += maxValues[i];
                if (maxValues[i] != 0.0)
                {
                    order[nUsedOctaves] = i;
                    for (int j = nUsedOctaves - 1; j >= 0; j--)
                    {
                        if (maxValues[order[j + 1]] > maxValues[order[j]])
                            (order[j + 1], order[j]) = (order[j], order[j + 1]);
                    }

                    nUsedOctaves++;
                }
            }

            this.BoundMin = NoiseValueCurve(-bound);
            this.BoundMax = NoiseValueCurve(bound);

            OctaveEntry[] entries = threadLocalOctaveEntries;
            if (entries == null || entries.Length < nUsedOctaves)
            {
                entries = new OctaveEntry[nUsedOctaves];
                threadLocalOctaveEntries = entries;
            }

            PastEvaluation[] pastEvals = threadLocalPastEvaluations;
            if (pastEvals == null || pastEvals.Length < nUsedOctaves)
            {
                pastEvals = new PastEvaluation[nUsedOctaves];
                threadLocalPastEvaluations = pastEvals;
            }

            this.orderedOctaveEntries = entries;
            this.pastEvaluations = pastEvals;
            this.usedOctaveCount = nUsedOctaves;

            double uncertaintySum = 0.0;
            for (int k = nUsedOctaves - 1; k >= 0; k--)
            {
                int l = order[k];
                uncertaintySum += maxValues[l];
                double thisOctaveFrequency = terrainNoise.frequencies[l];
                entries[k] = new OctaveEntry
                {
                    Seed = terrainNoise.octaveSeeds[l],
                    X = noiseX * thisOctaveFrequency,
                    Z = noiseZ * thisOctaveFrequency,
                    FrequencyY = thisOctaveFrequency * relativeYFrequency,
                    Amplitude = amplitudes[l] * ValueMultiplier,
                    Threshold = thresholds[l] * 1.2000000000000002, // Probably some fraction in the original code
                    SmoothingFactor = amplitudes[l] * thisOctaveFrequency * 3.5,
                    StopBound = uncertaintySum,
                    OriginalOctaveIndex = l
                };
                pastEvals[k] = new PastEvaluation { Y = double.NaN };
            }
        }

        public readonly double NoiseSign(double y, double inverseCurvedThresholder)
        {
            double value = inverseCurvedThresholder;
            double lowerBound = inverseCurvedThresholder;
            double upperBound = inverseCurvedThresholder;

            int i = 0;
            while (i < this.usedOctaveCount && (upperBound <= 0.0 || lowerBound >= 0.0))
            {
                ref OctaveEntry octaveEntry = ref this.orderedOctaveEntries[i];
                if (lowerBound >= octaveEntry.StopBound)
                    return lowerBound;

                if (upperBound <= -octaveEntry.StopBound)
                    return upperBound;

                double evalY = y * octaveEntry.FrequencyY;
                double deltaY = Math.Abs(this.pastEvaluations[i].Y - evalY);
                lowerBound += ApplyThresholding(Math.Max(-1.0, this.pastEvaluations[i].Value - (deltaY * 5.0)) * octaveEntry.Amplitude, octaveEntry.Threshold, octaveEntry.SmoothingFactor);
                upperBound += ApplyThresholding(Math.Min(1.0, this.pastEvaluations[i].Value + (deltaY * 5.0)) * octaveEntry.Amplitude, octaveEntry.Threshold, octaveEntry.SmoothingFactor);
                i++;
            }

            for (int j = 0; j < this.usedOctaveCount; j++)
            {
                ref OctaveEntry octaveEntry = ref this.orderedOctaveEntries[j];
                if (value >= octaveEntry.StopBound || value <= -octaveEntry.StopBound)
                    break;

                double evalY = y * octaveEntry.FrequencyY;
                double noiseValue = (double)NewSimplexNoiseLayer.Evaluate_ImprovedXZ(octaveEntry.Seed, octaveEntry.X, evalY, octaveEntry.Z);
                this.pastEvaluations[j].Value = noiseValue;
                this.pastEvaluations[j].Y = evalY;

                value += ApplyThresholding(noiseValue * octaveEntry.Amplitude, octaveEntry.Threshold, octaveEntry.SmoothingFactor);
            }

            return value;
        }

        private readonly OctaveEntry[] orderedOctaveEntries;
        private readonly PastEvaluation[] pastEvaluations;
        private readonly int usedOctaveCount;

        private struct OctaveEntry
        {
            public long Seed;
            public double X;
            public double FrequencyY;
            public double Z;
            public double Amplitude;
            public double Threshold;
            public double SmoothingFactor;
            public double StopBound;
            public int OriginalOctaveIndex;
        }

        private struct PastEvaluation
        {
            public double Value;
            public double Y;
        }
    }
}
