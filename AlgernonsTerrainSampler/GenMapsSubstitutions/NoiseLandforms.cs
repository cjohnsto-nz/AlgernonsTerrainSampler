using System.Threading;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace AlgernonsTerrainSampler.GenMapsSubstitutions;

public static class NoiseLandforms
{
    private static readonly Lock _loadLock = new();

    public static LandformsWorldProperty LandformsProperty { get; set; }

    public static void LoadLandforms(ICoreServerAPI api)
    {
        if (LandformsProperty != null)
            return;

        lock (_loadLock)
        {
            if (LandformsProperty != null)
                return;

            LandformsProperty = api.Assets.Get("worldgen/landforms.json").ToObject<LandformsWorldProperty>(null);

            int quantityMutations = 0;
            for (int i = 0; i < LandformsProperty.Variants.Length; i++)
            {
                LandformVariant variant = LandformsProperty.Variants[i];
                variant.index = i;
                variant.Init(api.WorldManager, i);
                if (variant.Mutations != null)
                    quantityMutations += variant.Mutations.Length;
            }

            LandformsProperty.LandFormsByIndex = new LandformVariant[quantityMutations + LandformsProperty.Variants.Length];
            for (int j = 0; j < LandformsProperty.Variants.Length; j++)
                LandformsProperty.LandFormsByIndex[j] = LandformsProperty.Variants[j];

            int nextIndex = LandformsProperty.Variants.Length;
            for (int k = 0; k < LandformsProperty.Variants.Length; k++)
            {
                LandformVariant variant2 = LandformsProperty.Variants[k];
                if (variant2.Mutations != null)
                {
                    for (int l = 0; l < variant2.Mutations.Length; l++)
                    {
                        LandformVariant variantMutation = variant2.Mutations[l];
                        variantMutation.TerrainOctaves ??= variant2.TerrainOctaves;
                        variantMutation.TerrainOctaveThresholds ??= variant2.TerrainOctaveThresholds;
                        variantMutation.TerrainYKeyPositions ??= variant2.TerrainYKeyPositions;
                        variantMutation.TerrainYKeyThresholds ??= variant2.TerrainYKeyThresholds;
                        LandformsProperty.LandFormsByIndex[nextIndex] = variantMutation;
                        variantMutation.Init(api.WorldManager, nextIndex);
                        nextIndex++;
                    }
                }
            }
        }
    }
}
