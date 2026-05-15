using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace AlgernonsTerrainSampler;

public static class Mapping
{
    // 32x32 chunks
    public const int CHUNK_SIZE = SmallChunkMapCoordinate.CHUNK_SIZE;

    public readonly struct RegionMapCorners
    {
        public readonly int UpLeft;
        public readonly int UpRight;
        public readonly int BotLeft;
        public readonly int BotRight;

        public RegionMapCorners(IntDataMap2D regionMap, int regionChunkSize, ChunkInRegionMapCoordinate chunkInRegionCoordinate)
        {
            if (regionMap == null || regionMap.Data.Length <= 0)
                return; // Empty map with all 4 corners being 0

            float factor = (float)regionMap.InnerSize / regionChunkSize;
            int left  = (int)(chunkInRegionCoordinate.X * factor);
            int right = (int)((chunkInRegionCoordinate.X * factor) + factor);
            int top   = (int)(chunkInRegionCoordinate.Z * factor);
            int bottom   = (int)((chunkInRegionCoordinate.Z * factor) + factor);

            this.UpLeft   = regionMap.GetUnpaddedInt(left,  top);
            this.UpRight  = regionMap.GetUnpaddedInt(right, top);
            this.BotLeft  = regionMap.GetUnpaddedInt(left,  bottom);
            this.BotRight = regionMap.GetUnpaddedInt(right, bottom);
        }

        public readonly bool IsEmpty => this.UpLeft == 0 && this.UpRight == 0 && this.BotLeft == 0 && this.BotRight == 0;

        public readonly float BiLerp(float x, float z)
            => GameMath.BiLerp(this.UpLeft, this.UpRight, this.BotLeft, this.BotRight, x, z);

        public readonly int BiLerpRgbColor(float x, float z)
            => GameMath.BiLerpRgbColor(x, z, this.UpLeft, this.UpRight, this.BotLeft, this.BotRight);
    }

    public static class ThreadSafeRegionCache
    {
        private const int REGION_MAP_CACHE_SIZE_LIMIT = 512;

        private static Lazy<MemoryCache> _MasterCache = NewMasterCache();

        private static Lazy<MemoryCache> NewMasterCache() => new(
            () => new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = REGION_MAP_CACHE_SIZE_LIMIT,
                CompactionPercentage = 0.10
            }),
            LazyThreadSafetyMode.ExecutionAndPublication);

        private static MemoryCache MasterCache => _MasterCache.Value;

        private const int LockStripeCount = 256;
        private static readonly object[] regionLocks = [.. Enumerable.Range(0, LockStripeCount).Select(_ => new object())];
        private static object GetRegionLock(object masterKey) => regionLocks[(masterKey.GetHashCode() & int.MaxValue) % LockStripeCount];

        public static IntDataMap2D GetRegionMap(
            RegionMapCoordinate regionCoordinate,
            int regionSize,
            int padding,
            int mapScale,
            MapLayerBase mapGen,
            string mapType)
        {
            if (mapGen == null)
                throw new ArgumentNullException(nameof(mapGen), $"Map generator for '{mapType}' is null.");

            object masterKey = (mapType, regionCoordinate.X, regionCoordinate.Z, mapGen.GetType());

            try
            {
                if (MasterCache.TryGetValue(masterKey, out IntDataMap2D masterMap))
                    return masterMap;

                object regionLock = GetRegionLock(masterKey);

                lock (regionLock)
                {
                    if (MasterCache.TryGetValue(masterKey, out masterMap))
                        return masterMap;

                    masterMap = GenerateNewMap(regionCoordinate, regionSize, padding, mapScale, mapGen);

                    _ = MasterCache.Set(masterKey, masterMap, new MemoryCacheEntryOptions
                    {
                        Size = 1,
                        SlidingExpiration = TimeSpan.FromMinutes(30)
                    });
                }

                return masterMap;
            }
            catch (ObjectDisposedException)
            {
                // Cache was disposed during shutdown so regenerate it.
                return GenerateNewMap(regionCoordinate, regionSize, padding, mapScale, mapGen);
            }
        }

        private static IntDataMap2D GenerateNewMap(
            RegionMapCoordinate regionCoordinate,
            int regionSize,
            int padding,
            int mapScale,
            MapLayerBase mapGen)
        {
            IntDataMap2D map = new();
            int noiseSize = regionSize / mapScale;

            lock (mapGen)
            {
                map.Data = mapGen.GenLayer(
                    (regionCoordinate.X * noiseSize) - padding,
                    (regionCoordinate.Z * noiseSize) - padding,
                    noiseSize + (2 * padding),
                    noiseSize + (2 * padding));
            }

            map.Size = noiseSize + (2 * padding);
            map.TopLeftPadding = padding;
            map.BottomRightPadding = padding;

            return map;
        }

        public static void Dispose()
        {
            if (_MasterCache.IsValueCreated)
                _MasterCache.Value.Dispose();
            _MasterCache = NewMasterCache();
        }
    }
}
