using Evix.Voxel.Collections;
using Evix.Voxel.Collections.Storage;
using Meeptech;

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
        // ignore events
        default:
          return;
      }
    }
  }
}
