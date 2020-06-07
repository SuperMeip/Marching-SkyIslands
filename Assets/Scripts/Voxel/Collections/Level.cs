using System.IO;
using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Evix.Voxel.Collections.Storage;
using Evix.Voxel.Generation.Sources;
using Evix.Voxel.Generation.Mesh;
using UnityEngine;

namespace Evix.Voxel.Collections {

  /// <summary>
  /// A collection of chunks, making an enclosed world in game
  /// </summary>
  public abstract class Level<VoxelStorageType> : ILevel
    where VoxelStorageType : IVoxelStorage {

    /// <summary>
    /// The size of a voxel 'block', in world
    /// </summary>
    public const float BlockSize = 1.0f;

    /// <summary>
    /// The width of the active chunk area in chunks
    /// </summary>
    public const int MeshedChunkDiameter = 15;

    /// <summary>
    /// The buffer diameter around rendered chunks to also load into memmory
    /// </summary>
    public const int ChunkLoadBuffer = 5;

    /// <summary>
    /// How many chunks down to load (temp);
    /// </summary>
    public const int ChunksBelowToMesh = 5;

    /// <summary>
    /// The width of the active chunk area in chunks
    /// </summary>
    public static readonly int LoadedChunkDiameter = MeshedChunkDiameter + ChunkLoadBuffer;

    /// <summary>
    /// The width of the active chunk area in chunks
    /// </summary>
    public static readonly int ChunksBelowToLoad = ChunksBelowToMesh + ChunkLoadBuffer;

    /// <summary>
    /// The height of the active chunk area in chunks
    /// </summary>
    public int LoadedChunkHeight {
      get => chunkBounds.y;
    }

    /// <summary>
    /// The overall bounds of the level, max x y and z
    /// </summary>
    public Coordinate chunkBounds {
      get;
      protected set;
    }

    /// <summary>
    /// The current center of all loaded chunks, usually based on player location
    /// </summary>
    public Coordinate focus {
      get;
      protected set;
    }

    /// <summary>
    /// The coordinates indicating the two chunks the extreems of what chunks are to be loaded from memmory:
    ///   0: south bottom west most loaded chunk
    ///   1: north top east most loaded chunk 
    /// </summary>
    protected Coordinate[] loadedChunkBounds;

    /// <summary>
    /// The coordinates indicating the two chunks the extreems of what chunks are to be meshed.
    ///   0: south bottom west most loaded chunk
    ///   1: north top east most loaded chunk 
    /// </summary>
    protected Coordinate[] meshedChunkBounds;

    /// <summary>
    /// The save path for levels.
    /// </summary>
    readonly string SavePath = "/leveldata/";

    /// <summary>
    /// The level seed
    /// </summary>
    int seed;

    /// <summary>
    /// The source used to load voxels for new chunks in this level
    /// </summary>
    IVoxelSource voxelSource;

    /// <summary>
    /// The generator to use for making the chunk meshes
    /// </summary>
    IVoxelMeshGenerator meshGenerator;

    /// <summary>
    /// Create a new level
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="chunkBounds">the max x y and z chunk sizes of the world</param>
    public Level(Coordinate chunkBounds, IVoxelSource voxelSource, IVoxelMeshGenerator meshGenerator) {
      this.voxelSource   = voxelSource;
      this.chunkBounds   = chunkBounds;
      this.meshGenerator = meshGenerator;
      seed = voxelSource.seed;
    }

    /// <summary>
    /// initialize this level with the center of loaded chunks fouced on the given location
    /// </summary>
    /// <param name="centerChunkLocation">the center point/focus of the loaded chunks, usually a player location</param>
    public abstract void initializeAround(Coordinate centerChunkLocation);

    /// <summary>
    /// Move the focus/central loaded point of the level by one chunk in the given direction
    /// </summary>
    /// <param name="newFocus">The new focal chunkLocation</param>
    public abstract void adjustFocusTo(Coordinate newFocus);

    /// <summary>
    /// Get the chunk at the given location (if it's loaded)
    /// </summary>
    /// <param name="chunkLocation">the location of the chunk to grab</param>
    /// <param name="withMeshes">get the chunk with it's mesh</param>
    /// <param name="withNeighbors">get the chunk with neighbors linked</param>
    /// <returns>the chunk data or null if there's none loaded</returns>
    public IVoxelChunk getChunk(Coordinate chunkLocation, bool withMeshes = false, bool withNeighbors = false, bool withNeighborsNeighbors = false, bool fullNeighborEncasement = false) {
      // just get an empty chunk for this one if this is out of bounds
      if (!chunkIsWithinLoadedBounds(chunkLocation)) {
        return Chunk.getEmptyChunk(withNeighbors);
      }
      if (chunkLocation.Equals(new Coordinate(42, 1, 55))) {
        Debug.Log("test");
      }

      IVoxelStorage voxels = getChunkVoxelData(chunkLocation);
      IVoxelChunk[] neighbors = null;

      if (withNeighbors) {
        neighbors = new IVoxelChunk[Directions.All.Length];
        foreach (Directions.Direction direction in Directions.All) {
          Coordinate neighborLocation = chunkLocation + direction.Offset;
          neighbors[direction.Value] = getChunk(neighborLocation, withMeshes, withNeighborsNeighbors, fullNeighborEncasement);
        }
      }

      return new Chunk(voxels, neighbors, withMeshes ? getChunkMesh(chunkLocation) : null);
    }

    /// <summary>
    /// Set the given voxeldata to the given chunk location in this level's active storage/memmory
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <param name="voxelData"></param>
    internal abstract void setChunkData(Coordinate chunkLocation, VoxelStorageType voxelData);

    /// <summary>
    /// Remove/nullify data for the chunk at the given location
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal abstract void removeChunk(Coordinate chunkLocation);

    /// <summary>
    /// Return if this level contains a loaded chunk mesh for the given chunk
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    internal abstract bool containsChunkMesh(Coordinate chunkLocation);

    /// <summary>
    /// add the loaded chunk mesh data to this level
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal abstract void setChunkMesh(Coordinate chunkLocation, IMesh chunkMesh);

    /// <summary>
    /// Remove/nullify data for the loaded chunk mesh at the given location
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal abstract void removeChunkMesh(Coordinate chunkLocation);

    /// <summary>
    /// get the chunk's voxel data for the given location
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected abstract IVoxelStorage getChunkVoxelData(Coordinate chunkLocation);

    /// <summary>
    /// Return the chunk mesh loaded for the given chunk
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected abstract IMesh getChunkMesh(Coordinate chunkLocation);

    /// <summary>
    /// Get the meshed chunk bounds for a given center point.
    /// Always trims to X,0,Z
    /// </summary>
    /// <param name="centerLocation"></param>
    protected abstract Coordinate[] getMeshedChunkBounds(Coordinate centerLocation);

    /// <summary>
    /// Get the loaded chunk bounds for a given center point.
    /// Always trims to X,0,Z
    /// </summary>
    /// <param name="centerLocation"></param>
    protected abstract Coordinate[] getLoadedChunkBounds(Coordinate centerLocation);

    /// <summary>
    /// Only to be used by jobs
    /// Save a chunk to file
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal void saveChunkDataToFile(Coordinate chunkLocation) {
      IVoxelChunk chunkData = getChunk(chunkLocation);
      if (!chunkData.isEmpty) {
        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(getChunkFileName(chunkLocation), FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, chunkData.voxels);
        stream.Close();
      }
    }

    /// <summary>
    /// Get the voxeldata for a chunk location from file
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    internal VoxelStorageType getVoxelDataForChunkFromFile(Coordinate chunkLocation) {
      IFormatter formatter = new BinaryFormatter();
      Stream readStream = new FileStream(getChunkFileName(chunkLocation), FileMode.Open, FileAccess.Read, FileShare.Read);
      VoxelStorageType voxelData = (VoxelStorageType)formatter.Deserialize(readStream);
      voxelData.isLoaded = true;
      readStream.Close();

      return voxelData;
    }

    /// <summary>
    /// Generate the chunk data for the chunk at the given location
    /// </summary>
    /// <param name="chunkLocation"></param>
    internal VoxelStorageType generateVoxelDataForChunk(Coordinate chunkLocation) {
      VoxelStorageType voxelData = (VoxelStorageType)Activator.CreateInstance(typeof(VoxelStorageType), Chunk.Diameter);
      voxelSource.generateAllAt(chunkLocation, voxelData);
      voxelData.isLoaded = true;

      return voxelData;
    }

    /// <summary>
    /// Generate the mesh for the voxeldata at the given chunk location
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    internal IMesh generateMeshDataForChunk(Coordinate chunkLocation) {
      if (chunkLocation.Equals(new Coordinate(42, 1, 55))) {
        Debug.Log("test");
      }
      IVoxelChunk chunk = getChunk(chunkLocation, false, true, true, true);
      if (!chunk.isEmpty) {
        return meshGenerator.generateMesh(chunk);
      }

      return default;
    }

    /// <summary>
    /// Get the file name a chunk is saved to based on it's location
    /// </summary>
    /// <param name="chunkLocation">the location of the chunk</param>
    /// <returns></returns>
    internal string getChunkFileName(Coordinate chunkLocation) {
      return SavePath + "/" + seed + "/" + chunkLocation.ToString() + ".evxch";
    }

    /// <summary>
    /// Get if the given chunkLocation is loaded
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected bool chunkIsWithinLoadedBounds(Coordinate chunkLocation) {
      return chunkLocation.isWithin(loadedChunkBounds[0], loadedChunkBounds[1]);
    }

    /// <summary>
    /// Get if the given chunkLocation should be meshed
    /// </summary>
    /// <param name="chunkLocation"></param>
    /// <returns></returns>
    protected bool chunkIsWithinkMeshedBounds(Coordinate chunkLocation) {
      return chunkLocation.isWithin(meshedChunkBounds[0], meshedChunkBounds[1]);
    }

    /// <summary>
    /// An event indicating a chunk has finished generating it's mesh and is ready to render
    /// </summary>
    public struct ChunkMeshGenerationFinishedEvent : IEvent {
      
      /// <summary>
      /// The chunk location of the chunk that's finished generating it's mesh
      /// </summary>
      public Coordinate chunkLocation {
        get;
      }

      /// <summary>
      /// The name of this event
      /// </summary>
      public string name => "Chunk Mesh Has Finished Generating";

      /// <summary>
      /// Create a new event indicating a chunk has finished generating it's mesh
      /// </summary>
      /// <param name="chunkLocation"></param>
      public ChunkMeshGenerationFinishedEvent(Coordinate chunkLocation) {
        this.chunkLocation = chunkLocation;
      }
    }

    /// <summary>
    /// An event indicating a chunk has finished generating it's mesh and is ready to render
    /// </summary>
    public struct ChunkDataLoadingFinishedEvent : IEvent {
      
      /// <summary>
      /// The chunk location of the chunk that's finished generating it's mesh
      /// </summary>
      public Coordinate chunkLocation {
        get;
      }

      /// <summary>
      /// The name of this event
      /// </summary>
      public string name => "Chunk Mesh Has Finished Generating";

      /// <summary>
      /// Create a new event indicating a chunk has finished generating it's mesh
      /// </summary>
      /// <param name="chunkLocation"></param>
      public ChunkDataLoadingFinishedEvent(Coordinate chunkLocation) {
        this.chunkLocation = chunkLocation;
      }
    }
  }

  public static class Vector3LevelUtilities {

    /// <summary>
    /// convert a world vector 3 to a level chunk location
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public static Coordinate toChunkLocation(this Vector3 location) {
      return (
        (int)location.x / Chunk.Diameter,
        (int)location.y / Chunk.Diameter,
        (int)location.z / Chunk.Diameter
      );
    }
  }
}