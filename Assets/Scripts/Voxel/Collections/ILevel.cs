using Evix.Voxel.Collections.Storage;

namespace Evix.Voxel.Collections {
  /// <summary>
  /// An interface for a level, used to load the block data for a level around a player/focus point
  /// </summary>
  public interface ILevel {

    /// <summary>
    /// The overall bounds of the level, max x y and z
    /// </summary>
    Coordinate chunkBounds {
      get;
    }

    /// <summary>
    /// The current focus the level is using
    /// </summary>
    Coordinate focus {
      get;
    }

    /// <summary>
    /// Get the chunk at the given location (if it's loaded)
    /// </summary>
    /// <param name="chunkLocation">the location of the chunk to grab</param>
    /// <param name="withMeshes">get the chunk with it's mesh</param>
    /// <param name="withNeighbors">get the chunk with neighbors linked</param>
    /// <param name="withNeighborsNeighbors">get the neightbors of the neighbors as well</param>
    /// <returns>the chunk data or null if there's none loaded</returns>
    IVoxelChunk getChunk(Coordinate chunkLocation, bool withMesh = false, bool withNeighbors = false, bool withNeighborsNeighbors = false, bool fullNeighborEncasement = false);

    /// <summary>
    /// </summary>
    /// <param name="centerChunkLocation">the center point/focus of the loaded chunks, usually a player location</param>
    void initializeAround(Coordinate centerChunkLocation);

    /// <summary>
    /// Adjust the level's loaded focus to a new location
    /// </summary>
    /// <param name="newFocus"></param>
    void adjustFocusTo(Coordinate newFocus);
  }
}