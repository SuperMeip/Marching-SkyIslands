using Evix.Voxel.Collections;
using Evix.Voxel.Collections.Storage;

namespace Evix.Controllers {

  /// <summary>
  /// Used to control a level in the game world
  /// </summary>
  public class LevelController : IObserver {
    
    /// <summary>
    /// The level this is managing
    /// </summary>
    ILevel level;

    /// <summary>
    /// Create a level controller
    /// </summary>
    public LevelController(ILevel level) {
      this.level = level;
    }

    /// <summary>
    /// Get notifications from other observers, EX:
    ///   block breaking and placing
    ///   player chunk location changes
    /// </summary>
    /// <param name="event">The event to notify this observer of</param>
    /// <param name="origin">(optional) the source of the event</param>
    public void notifyOf(IEvent @event, IObserver origin = null) {
      switch (@event) {
        // when a player spawns in the level
        case Player.SpawnEvent pse:
          level.initializeAround(pse.spawnLocation.toChunkLocation());
          break;
        // When the player moves to a new chunk, adjust the loaded level focus
        case Player.ChangeChunkLocationEvent pccle:
          level.adjustFocusTo(pccle.newChunkLocation);
          break;
        // when the level finishes loading a chunk's mesh. Render it in world
        case Level<VoxelDictionary>.ChunkMeshGenerationFinishedEvent lcmgfe:
          /*World.InstanciateObject(
            lcmgfe.chunkLocation.vec3 * Chunk.Diameter,
            level.getChunk(lcmgfe.chunkLocation, true).mesh
          );*/
          break;
        // ignore other events
        default:
          return;
      }
    }
  }
}
