#if DEBUG
using Vintagestory.API.MathTools;

namespace AlgernonsTerrainSampler.Terrain;

public partial class TerrainSamplerNormalizedSimplexFractalNoise
{
    public partial struct ColumnNoise
    {
        public readonly NoiseSignDiagnostics NoiseSignWithDebugDiag(double y, double inverseCurvedThresholder)
        {
            NoiseSignDiagnostics diagnostics = new()
            {
                InverseCurvedThreshold = inverseCurvedThresholder,
                OctaveCount = this.usedOctaveCount,
                OctaveDetails = new OctaveDiagnostic[this.usedOctaveCount]
            };

            double value = inverseCurvedThresholder;
            for (int j = 0; j < this.usedOctaveCount; j++)
            {
                ref OctaveEntry octaveEntry = ref this.orderedOctaveEntries[j];

                bool earlyExit = value >= octaveEntry.StopBound || value <= -octaveEntry.StopBound;

                double evalY = y * octaveEntry.FrequencyY;
                double noiseValue = (double)NewSimplexNoiseLayer.Evaluate_ImprovedXZ(octaveEntry.Seed, octaveEntry.X, evalY, octaveEntry.Z);
                double contribution = ApplyThresholding(noiseValue * octaveEntry.Amplitude, octaveEntry.Threshold, octaveEntry.SmoothingFactor);

                diagnostics.OctaveDetails[j] = new OctaveDiagnostic
                {
                    OctaveIndex = octaveEntry.OriginalOctaveIndex,
                    RawNoiseValue = noiseValue,
                    Amplitude = octaveEntry.Amplitude,
                    Threshold = octaveEntry.Threshold,
                    SmoothingFactor = octaveEntry.SmoothingFactor,
                    Contribution = contribution,
                    EarlyExitTriggered = earlyExit,
                    StopBound = octaveEntry.StopBound,
                    AccumulatedValueBefore = value
                };

                if (!earlyExit)
                    value += contribution;
            }

            diagnostics.FinalNoiseSign = value;
            diagnostics.IsSolid = value > 0;

            return diagnostics;
        }
    }
}

public struct OctaveDiagnostic
{
    public int OctaveIndex;
    public double RawNoiseValue;
    public double Amplitude;
    public double Threshold;
    public double SmoothingFactor;
    public double Contribution;
    public bool EarlyExitTriggered;
    public double StopBound;
    public double AccumulatedValueBefore;
}

public struct NoiseSignDiagnostics
{
    public double InverseCurvedThreshold;
    public int OctaveCount;
    public OctaveDiagnostic[] OctaveDetails;
    public double FinalNoiseSign;
    public bool IsSolid;
}
#endif
