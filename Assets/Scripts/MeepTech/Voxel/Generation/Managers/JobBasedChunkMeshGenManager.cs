using MeepTech.Events;
using MeepTech.GamingBasics;
using MeepTech.Voxel.Collections.Level;
using MeepTech.Voxel.Collections.Storage;
using MeepTech.Voxel.Generation.Mesh;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace MeepTech.Voxel.Generation.Managers {
  public class JobBasedChunkMeshGenManager : ChunkMeshGenerationManager {

    /// <summary>
    /// The current parent job, in charge of generating meshes for chunks in the load queue
    /// </summary>
    JGenerateChunkMeshes chunkMeshGenQueueManagerJob;

    /// <summary>
    /// construct
    /// </summary>
    public JobBasedChunkMeshGenManager(ILevel level, IChunkDataStorage chunkDataStorage, IVoxelMeshGenerator meshGenerator) : base(level, chunkDataStorage, meshGenerator) {
      chunkMeshGenQueueManagerJob = new JGenerateChunkMeshes(level, this);
    }

    /// <summary>
    /// Listen for events
    /// </summary>
    /// <param name="event"></param>
    /// <param name="origin"></param>
    public override void notifyOf(IEvent @event, IObserver origin = null) {
      switch (@event) {
        // if chunk data wasn't found in a file, lets generate it for them
        case ChunkFileDataLoadingManager<VoxelFlatArray>.ChunkDataLoadingFinishedEvent cfdlmcdlfe:
          chunkMeshGenQueueManagerJob.enQueue(new Coordinate[] { cfdlmcdlfe.chunkLocation });
          break;
        default:
          return;
      }
    }

    /// <summary>
    /// The job manager this manager uses
    /// </summary>
    class JGenerateChunkMeshes : ChunkQueueManagerJob<JobBasedChunkMeshGenManager> {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JGenerateChunkMeshes(ILevel level, JobBasedChunkMeshGenManager manager) : base(level, manager) {
        threadName = "Generate Chunk Mesh Manager";
      }

      /// <summary>
      /// get the child job given the values
      /// </summary>
      /// <param name="chunkLocation"></param>
      /// <param name="parentCancelationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkLocation) {
        return new JGenerateChunkMesh(this, chunkLocation);
      }

      /// <summary>
      /// remove empty chunks that have loaded from the mesh gen queue
      /// </summary>
      /// <param name="chunkLocation"></param>
      /// <returns></returns>
      protected override bool isAValidQueueItem(Coordinate chunkLocation) {
        IVoxelChunk chunk = level.getChunk(chunkLocation);
        // the chunk can't be loaded and empty, and it doesn't already have a loaded mesh.
        return !(chunk.isLoaded && chunk.isEmpty);
      }

      /// <summary>
      /// Don't generate a mesh until a chunk's data is loaded
      /// </summary>
      /// <param name="queueItem"></param>
      /// <returns></returns>
      protected override bool itemIsReady(Coordinate chunkLocation) {
        IVoxelChunk chunk = level.getChunk(chunkLocation, false, true, true, true);
        return chunk.isLoaded && chunk.neighborsNeighborsAreLoaded;
      }

      /// <summary>
      /// Sort the queue by distance from the focus of the level
      /// </summary>
      protected override void sortQueue() {
        Coordinate[] sortedQueue = queue.OrderBy(o => o.distance(level.focus)).ToArray();
        queue = new ConcurrentQueue<Coordinate>(sortedQueue);
      }

      /// <summary>
      /// Child job for doing work on the chunk columns
      /// </summary>
      protected class JGenerateChunkMesh : QueueTaskChildJob<Coordinate> {

        /// <summary>
        /// The level we're loading for
        /// </summary>
        protected new JGenerateChunkMeshes jobManager;

        /// <summary>
        /// Make a new job
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkLocation"></param>
        internal JGenerateChunkMesh(
          JGenerateChunkMeshes jobManager,
          Coordinate chunkLocation
        ) : base(chunkLocation, jobManager) {
          this.jobManager = jobManager;
          threadName = "Generate Mesh on Chunk: " + queueItem.ToString();
        }

        /// <summary>
        /// generate the chunk mesh if the level doesn't have it yet.
        /// </summary>
        protected override void doWork(Coordinate chunkLocation) {
          if (!jobManager.manager.chunkDataStorage.containsChunkMesh(chunkLocation)) {
            IMesh mesh = jobManager.manager.generateMeshDataForChunk(chunkLocation);
            if (!mesh.isEmpty && !jobManager.manager.chunkDataStorage.containsChunkMesh(chunkLocation)) {
              jobManager.manager.chunkDataStorage.setChunkMesh(chunkLocation, mesh);
              World.EventSystem.notifyChannelOf(
                new ChunkMeshGenerationFinishedEvent(chunkLocation),
                Evix.EventSystems.WorldEventSystem.Channels.TerrainGeneration
              );
            }
          }
        }
      }
    }
  }
}