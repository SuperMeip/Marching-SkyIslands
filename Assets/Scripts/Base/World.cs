using Evix.Controllers;
using Evix.Voxel;
using Evix.Voxel.Collections;
using Evix.Voxel.Collections.Storage;
using Evix.Voxel.Generation.Sources;
using Evix.Voxel.Generation.Mesh;
using System.Collections.Generic;
using UnityEngine;
using Evix.Controllers.Unity;
using MeepTech;

namespace Evix {

  /// <summary>
  /// Za warudo
  /// </summary>
  public class World {

    /// <summary>
    /// The current world
    /// </summary>
    public static World Current {
      get; 
    } = new World();

    /// <summary>
    /// The debugger used to interface with unity debugging.
    /// </summary>
    public static UnityDebugger Debugger {
      get;
    } = new UnityDebugger();

    /// <summary>
    /// the players in this world
    /// </summary>
    public Player[] players {
      get;
      private set;
    }

    /// <summary>
    /// The currently loaded level
    /// </summary>
    public ILevel activeLevel {
      get;
      private set;
    }

    /// <summary>
    /// The world controller.
    /// </summary>
    public UnityWorldController worldController;

    /// <summary>
    /// all observers currently listening
    /// </summary>
    List<IObserver> listeningObservers;

    /// <summary>
    /// The objects this world is managing
    /// </summary>
    List<GameObject> gameObjects;

    /// <summary>
    /// Make a new world
    /// </summary>
    World() {
      listeningObservers = new List<IObserver>();
      gameObjects = new List<GameObject>();
      players = new Player[2];
    }

    /// <summary>
    /// start test world
    /// </summary>
    public void initializeTestWorld(UnityLevelController levelController, IVoxelSource terrainSource) {
      players[0]                      = new Player();
      Coordinate chunkBounds          = (1000, 20, 1000);
      activeLevel                     = new ColumnLoadedLevel<VoxelDictionary>(
        chunkBounds,
        terrainSource,
        new MarchGenerator()
      );

      Coordinate spawn = (
        chunkBounds.x * Chunk.Diameter / 2,
        chunkBounds.y * Chunk.Diameter / 2,
        chunkBounds.z * Chunk.Diameter / 2
      );

      levelController.level = activeLevel;
      levelController.initialize();
      listeningObservers.Add(levelController);
      activeLevel.initializeAround(spawn / Chunk.Diameter);
    }

    /// <summary>
    /// start test world
    /// </summary>
    public void initializeSphereTest(UnityLevelController levelController) {
      players[0]                      = new Player();
      Coordinate chunkBounds          = (10, 10, 10);
      Coordinate spawn                = (
        chunkBounds.x * Chunk.Diameter / 2,
        5,
        chunkBounds.x * Chunk.Diameter / 2
      );
      activeLevel                     = new ColumnLoadedLevel<VoxelDictionary>(
        chunkBounds,
        new SphereSource(50, spawn),
        new MarchGenerator()
      );

      levelController.level = activeLevel;
      levelController.initialize();
      listeningObservers.Add(levelController);
      activeLevel.initializeAround(spawn / Chunk.Diameter);
    }

    /// <summary>
    /// Notify all listening observers of an event
    /// </summary>
    /// <param name="event">The event to notify all listening observers of</param>
    /// <param name="origin">(optional) the osurce of the event</param>
    public static void NotifyAllOf(IEvent @event, IObserver origin = null) {
      foreach (IObserver observer in Current.listeningObservers) {
        observer.notifyOf(@event, origin);
      }
    }

    /// <summary>
    /// Instanciate a new game object in the world
    /// </summary>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    public static GameObject InstanciateObject(GameObject gameObject) {
      Current.gameObjects.Add(gameObject);
      return gameObject;
    }

    /// <summary>
    /// Instanciate a new gameobject in this world
    /// </summary>
    /// <param name="location">Where to create the new object</param>
    /// <param name="mesh">The mesh to apply to this object</param>
    /*
    public static MeshedObject InstanciateObject(Vector3 location, IMesh mesh) {
      IGameObject newObject = new MeshedObject(location, GameResources.graphicsDevice, mesh, GameResources.effects);
      Current.gameObjects.Add(newObject);
      return newObject as MeshedObject;
    }*/
  }
}