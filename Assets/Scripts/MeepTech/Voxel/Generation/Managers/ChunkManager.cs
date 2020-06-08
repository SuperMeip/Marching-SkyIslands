using MeepTech.Events;
using MeepTech.Voxel.Collections.Level;

namespace MeepTech.Voxel.Generation.Managers {
  /// <summary>
  /// Manages some aspect of chunks for levels.
  /// </summary>
  public abstract class ChunkManager : IChunkManager {

    /// <summary>
    /// The level this data load manager is working for
    /// </summary>
    protected ILevel level;

    /// <summary>
    /// The chunk data storage this is managing loading for
    /// </summary>
    internal IChunkDataStorage chunkDataStorage;

    /// <summary>
    /// Construct
    /// </summary>
    /// <param name="level">The level this manager is managing for</param>
    public ChunkManager(ILevel level, IChunkDataStorage chunkDataStorage) {
      this.level = level;
      this.chunkDataStorage = chunkDataStorage;
    }

    /// <summary>
    /// Children should be observers
    /// </summary>
    /// <param name="event"></param>
    /// <param name="origin"></param>
    public abstract void notifyOf(IEvent @event, IObserver origin = null);
  }
}