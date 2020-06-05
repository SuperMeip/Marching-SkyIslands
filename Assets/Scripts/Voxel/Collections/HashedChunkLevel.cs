using System;
using System.Collections.Generic;
using Evix.Voxel.Collections.Storage;
using Evix.Voxel.Generation.Sources;
using Evix.Voxel.Generation.Mesh;

namespace Evix.Voxel.Collections {

  /// <summary>
  /// A level that uses a dictionary to organize it's chunks
  /// </summary>
  /// <typeparam name="VoxelStorageType"></typeparam>
  public abstract class HashedChunkLevel<VoxelStorageType> 
    : Level<VoxelStorageType> where VoxelStorageType : IVoxelStorage {

    /// <summary>
    /// The active chunks, stored by coordinate location
    /// </summary>
    Dictionary<long, VoxelStorageType> loadedChunks;

    /// <summary>
    /// the loaded chunk meshes
    /// </summary>
    Dictionary<long, IMesh> chunkMeshes;

    /// <summary>
    /// Construct
    /// </summary>
    /// <param name="chunkBounds"></param>
    /// <param name="voxelSource"></param>
    public HashedChunkLevel(
      Coordinate chunkBounds,
      IVoxelSource voxelSource,
      IVoxelMeshGenerator meshGenerator
    ) : base(chunkBounds, voxelSource, meshGenerator) {
      loadedChunks = new Dictionary<long, VoxelStorageType>(
        chunkBounds.x * chunkBounds.y * chunkBounds.z
      );
      chunkMeshes = new Dictionary<long, IMesh>(
        chunkBounds.x * chunkBounds.y * chunkBounds.z
      );
    }

    /// <summary>
    /// Get the chunk from the hash map
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected override IVoxelStorage getChunkVoxelData(Coordinate chunkLocation) {
      return chunkLocation.isWithin(chunkBounds)
        && chunkIsWithinLoadedBounds(chunkLocation)
        && loadedChunks.ContainsKey(GetChunkHash(chunkLocation))
          ? loadedChunks[GetChunkHash(chunkLocation)]
          : default;
    }

    /// <summary>
    /// Set the given set of voxel data to the given chunk location
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <param name="voxelData"></param>
    internal override void setChunkData(Coordinate chunkLocation, VoxelStorageType voxelData) {
      loadedChunks[GetChunkHash(chunkLocation)] = voxelData;
    }

    /// <summary>
    /// Remove the chunk at the given location
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal override void removeChunk(Coordinate chunkLocation) {
      loadedChunks.Remove(GetChunkHash(chunkLocation));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <param name="chunkMesh"></param>
    internal override void setChunkMesh(Coordinate chunkLocation, IMesh chunkMesh) {
      chunkMeshes[GetChunkHash(chunkLocation)] = chunkMesh;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <param name="chunkMesh"></param>
    internal override void removeChunkMesh(Coordinate chunkLocation) {
      chunkMeshes.Remove(GetChunkHash(chunkLocation));
    }

    /// <summary>
    /// if this contains the chunk mesh key
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <param name="chunkMesh"></param>
    internal override bool containsChunkMesh(Coordinate chunkLocation) {
      lock (chunkMeshes) {
        return chunkMeshes.ContainsKey(GetChunkHash(chunkLocation));
      }
    }

    /// <summary>
    /// Get the mesh for a chunk if one's loaded
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected override IMesh getChunkMesh(Coordinate chunkLocation) {
      return chunkLocation.isWithin(chunkBounds)
        && chunkIsWithinkMeshedBounds(chunkLocation)
        && containsChunkMesh(chunkLocation)
          ? chunkMeshes[GetChunkHash(chunkLocation)]
          : default;
    }

    /// <summary>
    /// Get the hash key for the chunk's location
    /// todo: add property longHash to coordinate
    /// </summary>
    /// <returns></returns>
    protected static long GetChunkHash(Coordinate chunkLocation) {
      long hash = 0;
      hash |= ((short)chunkLocation.x);
      hash |= (((short)chunkLocation.y) << 16);
      hash |= (((short)chunkLocation.z) << 24);

      return hash;
    }
  }
}