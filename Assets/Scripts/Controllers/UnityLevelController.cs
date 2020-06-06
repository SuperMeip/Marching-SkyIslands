using Evix.Voxel.Collections;
using Evix.Voxel.Collections.Storage;
using UnityEngine;
using System.Collections.Concurrent;
using Meeptech;
using Evix.Voxel;

namespace Evix.Controllers.Unity {

  /// <summary>
  /// Used to control a level in the game world
  /// </summary>
  public class UnityLevelController : MonoBehaviour, IObserver {

    /// <summary>
    /// Player controller for player 1
    /// </summary>
    public UnityEvixPlayerController player1;

    /// <summary>
    /// The prefab used to render a chunk in unity.
    /// </summary>
    public GameObject chunkObjectPrefab;

    /// <summary>
    /// The count of rendered chunks atm
    /// </summary>
    [ReadOnly] public int renderedChunksCount;

    /// <summary>
    /// The level this is managing
    /// </summary>
    [HideInInspector] public ILevel level;

    /// <summary>
    /// The level is loaded enough for the manager to begin working
    /// </summary>
    [HideInInspector] public bool isLoaded;

    /// <summary>
    /// Chunk controllers waiting for assignement and activation
    /// </summary>
    [HideInInspector] public ConcurrentDictionary<Vector3, bool> chunkControllerDeActivationTokens;

    /// <summary>
    /// Chunk controllers waiting for assignement and activation
    /// </summary>
    ConcurrentQueue<UnityChunkController> chunkControllerActivationQueue;

    /// <summary>
    /// The pool of prefabs
    /// </summary>
    UnityChunkController[] chunkControllerPool;

    /// <summary>
    /// Used for drawing in the editor which chunks are loaded.
    /// </summary>
    ConcurrentDictionary<Vector3, bool> loadedChunkLocations;


    ///// UNITY FUNCTIONS

    void Update() {
      if (isLoaded) {
        /// load new chunks
        chunkControllerActivationQueue = chunkControllerActivationQueue ?? new ConcurrentQueue<UnityChunkController>();
        if (chunkControllerActivationQueue.Count > 0 && chunkControllerActivationQueue.TryPeek(out UnityChunkController chunkController)) {
          World.Debugger.log($"chunkcontoller found {chunkController.name} waiting to attach and activate for chunk at: {chunkController.chunkLocation.ToString()}");
          if (!chunkController.isMeshed) {
            World.Debugger.log($"chunkcontoller: {chunkController.name} has not attachecd mesh for chunk at: {chunkController.chunkLocation.ToString()}. Trying to attach");
            chunkController.updateMeshWithChunkData();
            World.Debugger.log($"chunkcontoller: {chunkController.name} has finished attacheing mesh for chunk at: {chunkController.chunkLocation.ToString()}!");
          } else if (!chunkController.gameObject.activeSelf) {
            World.Debugger.log($"chunkcontoller: {chunkController.name}'s gamobject is not active, but has a chunk attached for chunk at: {chunkController.chunkLocation.ToString()}. Trying to enable");
            chunkController.setObjectActive();
            renderedChunksCount++;
            World.Debugger.log($"Sucess, chunkcontoller: {chunkController.name}'s gamobject is now active {chunkController.gameObject.activeSelf}. Tryign to remove from queue...(({chunkController.chunkLocation.ToString()}))");
            if (chunkControllerActivationQueue.TryDequeue(out _)) {
              World.Debugger.log($"Successfully removed chunk for chunk at {chunkController.chunkLocation.ToString()} from the activation queue.");
            } else {
              World.Debugger.log($"Could not remove chunk for chunk at {chunkController.chunkLocation.ToString()} from the activation queue.");
            }
          } else {
            World.Debugger.log($"Tryign to remove from queue chunk at: (({chunkController.chunkLocation.ToString()})) that is active and meshed but still chillin inthe queue...");
            if (chunkControllerActivationQueue.TryDequeue(out _)) {
              World.Debugger.log($"Successfully removed chunk for chunk at {chunkController.chunkLocation.ToString()} from the activation queue. ((+))");
            } else {
              World.Debugger.log($"Could not remove active meshed chunk from queue for chunk at {chunkController.chunkLocation.ToString()}, going around again.");
            }
          }
        }
      }
    }

    ///// GUI FUNCTIONS

    void OnDrawGizmos() {
      // ignore gizmo if we have no level to draw
      if (!isLoaded || level == null) {
        return;
      }
      /// draw the focus
      Vector3 focalWorldPoint = level.focus.vec3 * Chunk.Diameter;
      Gizmos.color = Color.yellow;
      Gizmos.DrawWireSphere(focalWorldPoint, Chunk.Diameter / 2);
      /// draw the meshed chunk area
      float loadedChunkHeight = level.chunkBounds.y * Chunk.Diameter;
      focalWorldPoint.y = loadedChunkHeight / 2;
      float meshedChunkDiameter = Level<IVoxelStorage>.MeshedChunkDiameter * Chunk.Diameter;
      Gizmos.color = Color.blue;
      Gizmos.DrawWireCube(focalWorldPoint, new Vector3(meshedChunkDiameter, loadedChunkHeight, meshedChunkDiameter));
      /// draw the loaded chunk area
      float loadedChunkDiameter = Level<IVoxelStorage>.LoadedChunkDiameter * Chunk.Diameter;
      Gizmos.color = Color.yellow;
      Gizmos.DrawWireCube(focalWorldPoint, new Vector3(loadedChunkDiameter, loadedChunkHeight, loadedChunkDiameter));
    }

    void OnDrawGizmosSelected() {
      // ignore gizmo if we have no level to draw
      if (!isLoaded || level == null) {
        return;
      }      
      /// draw loaded chunks.
        Gizmos.color = Color.white;
      foreach (var loadedChunkLocation in loadedChunkLocations) {
        if (loadedChunkLocation.Value) {
          Gizmos.DrawSphere(loadedChunkLocation.Key * Chunk.Diameter, 1);
        }
      }
    }

    ///// PUBLIC FUNCTIONS

    /// <summary>
    /// Initilize this chunk controller for it's provided level.
    /// </summary>
    public void initialize() {
      if (chunkObjectPrefab == null) {
        Debug.LogError("UnityLevelController Missing chunk prefab, can't work");
      } else if (level == null) {
        Debug.LogError("No level provided by world. Did you hook this level controller up to the world controller?");
      } else if (player1 == null) {
        Debug.LogError("No player 1 provided by world. Did you hook this level controller up to the player controller?");
      } else {
        chunkControllerActivationQueue = new ConcurrentQueue<UnityChunkController>();
        chunkControllerDeActivationTokens = new ConcurrentDictionary<Vector3, bool>();
        loadedChunkLocations = new ConcurrentDictionary<Vector3, bool>();
        chunkControllerPool = new UnityChunkController[Level<IVoxelStorage>.MeshedChunkDiameter * Level<IVoxelStorage>.MeshedChunkDiameter * level.chunkBounds.y];

        /// get all the chunk controllers
        for (int index = 0; index < chunkControllerPool.Length; index++) {
          // for each chunk we want to be able to render at once, create a new pooled gameobject for it with the prefab that has a unitu chunk controller on it
          GameObject chunkObject = Instantiate(chunkObjectPrefab);
          chunkObject.transform.parent = gameObject.transform;
          UnityChunkController chunkController = chunkObject.GetComponent<UnityChunkController>();
          if (chunkController == null) {
            Debug.LogError($"No chunk controller on {chunkObject.name}");
          } else {
            chunkControllerPool[index] = chunkController;
            chunkController.levelController = this;
            chunkObject.SetActive(false);
          }
        }
        isLoaded = true;

        ///  activate the player controller, spawn them in the middle of the level
        /*player1.spawn((
          level.chunkBounds.x * Chunk.Diameter / 2,
          level.chunkBounds.y * Chunk.Diameter / 2,
          level.chunkBounds.z * Chunk.Diameter / 2
        ));*/
      }
    }

    /// <summary>
    /// Clear all rendered and stored level data that we have.
    /// </summary>
    public void clearAll() {
      level = null;
      isLoaded = false;
      chunkControllerActivationQueue = null;
      foreach (UnityChunkController chunkController in chunkControllerPool) {
        if (chunkController != null) {
          Destroy(chunkController.gameObject);
        }
      }
    }

    /// <summary>
    /// Get notifications from other observers, EX:
    ///   block breaking and placing
    ///   player chunk location changes
    /// </summary>
    /// <param name="event">The event to notify this observer of</param>
    /// <param name="origin">(optional) the source of the event</param>
    public void notifyOf(IEvent @event, IObserver origin = null) {
      // ignore events if we have no level to control
      if (!isLoaded || level == null) {
        return;
      }

      switch (@event) {
        // when a player spawns in the level
        case Player.SpawnEvent pse:
          level.initializeAround(pse.spawnLocation / Chunk.Diameter);
          break;
        // When the player moves to a new chunk, adjust the loaded level focus
        case Player.ChangeChunkLocationEvent pccle:
          level.adjustFocusTo(pccle.newChunkLocation);
          break;
        // when the level finishes loading a chunk's mesh. Render it in world
        case Level<VoxelDictionary>.ChunkMeshReadyForRenderEvent lcmrfre:
          // first, cancel any tokens trying to de-activate the mesh already existing for this chunk.
          if (chunkControllerDeActivationTokens.TryGetValue(lcmrfre.chunkLocation.vec3, out _)) {
            chunkControllerDeActivationTokens.TryRemove(lcmrfre.chunkLocation.vec3, out _);
          }
          // don't render a chunk we already have
          if (chunkControllerPoolAlreadyContains(lcmrfre.chunkLocation.vec3)) {
            return;
          }
          UnityChunkController unusedChunkController = getUnusedChunkController();
          if (unusedChunkController == null) {
            Debug.LogError($"No free chunk controller found for {lcmrfre.chunkLocation.ToString()}");
          } else {
            IVoxelChunk chunk = level.getChunk(lcmrfre.chunkLocation, true);
            if (unusedChunkController.setChunkToRender(chunk, lcmrfre.chunkLocation.vec3)) {
              chunkControllerActivationQueue.Enqueue(unusedChunkController);
            }
          }
          break;
        // when a chunk has been loaded fully into memory.
        case Level<VoxelDictionary>.ChunkDataLoadingFinishedEvent lcdlfe:
          loadedChunkLocations.TryAdd(lcdlfe.chunkLocation.vec3, true);
          break;
        // when a chunk is unloaded from memmory
        case Level<VoxelDictionary>.ChunkDataUnloadingFinishedEvent lcdufe:
          if (loadedChunkLocations.TryGetValue(lcdufe.chunkLocation.vec3, out bool isLoaded)) {
            if (isLoaded
              && loadedChunkLocations.TryUpdate(lcdufe.chunkLocation.vec3, false, true)
              && loadedChunkLocations.TryRemove(lcdufe.chunkLocation.vec3, out _)
              ) {
              return;
            }
            if (!isLoaded) {
              loadedChunkLocations.TryRemove(lcdufe.chunkLocation.vec3, out _);
            }
          }
          break;
        // if a chunk gets out of the render zone, store a deactivation token for it
        case Level<VoxelDictionary>.ChunkOutOfRenderZoneEvent lcoorze:
          foreach (Coordinate coordinate in lcoorze.chunkLocations) {
            chunkControllerDeActivationTokens.TryAdd(coordinate.vec3, true);
          }
          break;
        default:
          return;
      }
    }

    ///// SUB FUNCTIONS

    /// <summary>
    /// Get an unused chunk controller from the pool we made
    /// </summary>
    /// <returns></returns>
    UnityChunkController getUnusedChunkController() {
      foreach(UnityChunkController chunkController in chunkControllerPool) {
        if (chunkController != null && !chunkController.isActive) {
          chunkController.isActive = true;
          return chunkController;
        }
      }

      return null;
    }

    /// <summary>
    /// Check if the chunk is already being controller by an active chunk controller
    /// </summary>
    /// <param name="coordinate"></param>
    /// <returns></returns>
    bool chunkControllerPoolAlreadyContains(Vector3 coordinate) {
      foreach (UnityChunkController chunkController in chunkControllerPool) {
        if (chunkController != null && !chunkController.isActive && chunkController.chunkLocation == coordinate) {
          return true;
        }
      }

      return false;
    }
  }
}
