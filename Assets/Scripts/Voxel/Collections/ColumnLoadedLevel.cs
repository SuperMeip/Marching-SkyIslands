using System.Collections.Generic;
using System;
using System.IO;
using Evix.Voxel.Collections.Storage;
using Evix.Voxel.Generation.Sources;
using MeepTech.Jobs;
using Evix.Voxel.Generation.Mesh;
using System.Linq;
using System.Collections.Concurrent;

namespace Evix.Voxel.Collections {

  /// <summary>
  /// A type of level loaded column by column
  /// </summary>
  /// <typeparam name="VoxelStorageType"></typeparam>
  public class ColumnLoadedLevel<VoxelStorageType> 
    : HashedChunkLevel<VoxelStorageType> where VoxelStorageType : IVoxelStorage {

    /// <summary>
    /// The maximum number of chunk load jobs that can run for one queue manager simultaniously
    /// </summary>
    const int MaxChunkLoadingJobsCount = 10;

    /// <summary>
    /// The current parent job, in charge of loading the chunks in the load queue
    /// </summary>
    JLoadChunks chunkLoadQueueManagerJob;

    /// <summary>
    /// The current parent job, in charge of loading the chunks in the load queue
    /// </summary>
    JUnloadChunks chunkUnloadQueueManagerJob;

    /// <summary>
    /// The current parent job, in charge of generating meshes for chunks in the load queue
    /// </summary>
    JGenerateChunkMeshes chunkMeshGenQueueManagerJob;

    /// <summary>
    /// construct
    /// </summary>
    /// <param name="chunkBounds"></param>
    /// <param name="voxelSource"></param>
    public ColumnLoadedLevel(Coordinate chunkBounds, IVoxelSource voxelSource, IVoxelMeshGenerator meshGenerator) : base(chunkBounds, voxelSource, meshGenerator) {
      chunkLoadQueueManagerJob   = new JLoadChunks(this);
      chunkUnloadQueueManagerJob = new JUnloadChunks(this);
      chunkMeshGenQueueManagerJob = new JGenerateChunkMeshes(this);
    }

    /// <summary>
    /// initialize this level with the center of loaded chunks fouced on the given location
    /// </summary>
    /// <param name="centerChunkLocation">the center point/focus of the loaded chunks, usually a player location</param>
    public override void initializeAround(Coordinate centerChunkLocation) {
      focus = centerChunkLocation;
      loadedChunkBounds = getLoadedChunkBounds(focus);
      Coordinate[] chunkColumnsToLoad = Coordinate.GetAllPointsBetween(loadedChunkBounds[0].replaceY(0), loadedChunkBounds[1].replaceY(1));
      addChunkColumnsToLoadingQueue(chunkColumnsToLoad);

      meshedChunkBounds = getMeshedChunkBounds(focus);
      Coordinate[] chunksToMeshGen = Coordinate.GetAllPointsBetween(meshedChunkBounds[0], meshedChunkBounds[1]);
      addChunksToMeshGenQueue(chunksToMeshGen);
      isInitialized = true;
    }

    /// <summary>
    /// Adjust the focus chunk to a new location
    /// </summary>
    /// <param name="newFocus">The new focal chunkLocation</param>
    public override void adjustFocusTo(Coordinate newFocus) {
      if (isInitialized && !newFocus.Equals(focus)) {
        Coordinate[] newLoadedChunkBounds = getLoadedChunkBounds(newFocus);
        Coordinate[] newMeshedChunkBounds = getMeshedChunkBounds(newFocus);
        Coordinate[] chunkColumnsToLoad = Coordinate.GetPointDiff(newLoadedChunkBounds, loadedChunkBounds);
        Coordinate[] chunkColumnsToUnload = Coordinate.GetPointDiff(loadedChunkBounds, newLoadedChunkBounds);
        Coordinate[] chunkColumnsGenerateMeshesFor = Coordinate.GetPointDiff(newMeshedChunkBounds, meshedChunkBounds);
        Coordinate[] chunkColumnsToDeRender = Coordinate.GetPointDiff(meshedChunkBounds, newMeshedChunkBounds);

        // update the values after getting the point diffs
        meshedChunkBounds = newMeshedChunkBounds;
        loadedChunkBounds = newLoadedChunkBounds;

        // queue the collected values
        addChunkColumnsToLoadingQueue(chunkColumnsToLoad);
        addChunkColumnsToUnloadingQueue(chunkColumnsToUnload);
        addChunksToMeshGenQueue(chunkColumnsGenerateMeshesFor);
        alertOfChunksReadyToDeRedner(chunkColumnsToDeRender);
      }
    }

    /// <summary>
    /// Get the loaded chunk bounds for a given center point.
    /// Always trims to X,0,Z
    /// </summary>
    /// <param name="centerLocation"></param>
    protected override Coordinate[] getLoadedChunkBounds(Coordinate centerLocation) {
      return new Coordinate[] {
        (
          Math.Max(centerLocation.x - LoadedChunkDiameter / 2, 0),
          Math.Max(centerLocation.y - ChunksBelowToLoad, 0),
          Math.Max(centerLocation.z - LoadedChunkDiameter / 2, 0)
        ),
        (
          Math.Min(centerLocation.x + LoadedChunkDiameter / 2, chunkBounds.x),
          chunkBounds.y,
          Math.Min(centerLocation.z + LoadedChunkDiameter / 2, chunkBounds.z)
        )
      };
    }

    /// <summary>
    /// Get the rendered chunk bounds for a given center point.
    /// Always trims to X,0,Z
    /// </summary>
    /// <param name="centerLocation"></param>
    protected override Coordinate[] getMeshedChunkBounds(Coordinate centerLocation) {
      return new Coordinate[] {
        (
          Math.Max(centerLocation.x - MeshedChunkDiameter / 2, 0),
          Math.Max(centerLocation.y - ChunksBelowToMesh, 0),
          Math.Max(centerLocation.z - MeshedChunkDiameter / 2, 0)
        ),
        (
          Math.Min(centerLocation.x + MeshedChunkDiameter / 2, chunkBounds.x),
          chunkBounds.y,
          Math.Min(centerLocation.z + MeshedChunkDiameter / 2, chunkBounds.z)
        )
      };
    }

    /// <summary>
    /// Add multiple chunk column locations to the load queue and run it
    /// </summary>
    /// <param name="chunkColumnLocations">the x,z values of the chunk columns to load</param>
    protected void addChunkColumnsToLoadingQueue(Coordinate[] chunkColumnLocations) {
      chunkLoadQueueManagerJob.enQueue(chunkColumnLocations);
    }

    /// <summary>
    /// Add multiple chunk column locations to the unload queue and run it
    /// </summary>
    /// <param name="chunkColumnLocations">the x,z values of the chunk columns to unload</param>
    protected void addChunkColumnsToUnloadingQueue(Coordinate[] chunkColumnLocations) {
      chunkLoadQueueManagerJob.deQueue(chunkColumnLocations);
      chunkUnloadQueueManagerJob.enQueue(chunkColumnLocations);
    }

    /// <summary>
    /// Add multiple chunks to the mesh generation queue
    /// </summary>
    /// <param name="chunkLocations"></param>
    protected void addChunksToMeshGenQueue(Coordinate[] chunkLocations) {
      chunkMeshGenQueueManagerJob.enQueue(chunkLocations);
    }

    /// <summary>
    /// Send event to derender chunks out of the mesh rendering zone
    /// </summary>
    /// <param name="chunksToDeRender"></param>
    protected void alertOfChunksReadyToDeRedner(Coordinate[] chunksToDeRender) {
      World.NotifyAllOf(new ChunkOutOfRenderZoneEvent(chunksToDeRender));
    }

    /// <summary>
    /// A job to load all chunks from the loading queue
    /// </summary>
    class JLoadChunks : LevelQueueManagerJob {

      /// <summary>
      /// The job for generating chunks from scratch
      /// </summary>
      JGenerateChunks chunkGenerationManagerJob;

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></pa
      public JLoadChunks(Level<VoxelStorageType> level) : base(level) {
        threadName = "Load Chunk Manager";
        chunkGenerationManagerJob = new JGenerateChunks(level);
      }

      /// <summary>
      /// Get the correct child job
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <param name="parentCancellationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JLoadChunkColumnFromFile(this, chunkColumnLocation);
      }

      /// <summary>
      /// Override to shift generational items to their own queue
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <returns></returns>
      protected override bool isAValidQueueItem(Coordinate chunkColumnLocation) {
        // if this doesn't have a loaded file, remove it from this queue and load it in the generation one
        if (!File.Exists(level.getChunkFileName(chunkColumnLocation))) {
          return false;
        }

        return base.isAValidQueueItem(chunkColumnLocation);
      }

      /// <summary>
      /// Do something on an invalid item before we toss it out
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      protected override void onQueueItemInvalid(Coordinate chunkColumnLocation) {
        chunkGenerationManagerJob.enQueue(new Coordinate[] { chunkColumnLocation });
      }

      /// <summary>
      /// Sort the queue by distance from the focus of the level
      /// </summary>
      protected override void sortQueue() {
        Coordinate[] sortedQueue = queue.OrderBy(o => o.distance(level.focus)).ToArray();
        queue = new ConcurrentQueue<Coordinate>(sortedQueue);
      }

      /// <summary>
      /// A Job for loading the data for a column of chunks into a level from file
      /// </summary>
      class JLoadChunkColumnFromFile : ChunkColumnWorkerJob {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkColumnLocation"></param>
        /// <param name="parentCancellationSources"></param>
        internal JLoadChunkColumnFromFile(
          JLoadChunks jobManager,
          Coordinate chunkColumnLocation
        ) : base(jobManager, chunkColumnLocation) {
          threadName = "Load Column: " + chunkColumnLocation;
        }

        /// <summary>
        /// Threaded function, loads all the voxel data for this chunk
        /// </summary>
        protected override void doWorkOnChunk(Coordinate chunkLocation) {
          if (jobManager.level.getChunk(chunkLocation).isEmpty) {
            VoxelStorageType voxelData = jobManager.level.getVoxelDataForChunkFromFile(chunkLocation);
            jobManager.level.setChunkData(chunkLocation, voxelData);
            World.NotifyAllOf(new ChunkDataLoadingFinishedEvent(chunkLocation));
          }
        }
      }
    }

    /// <summary>
    /// A job to load all chunks from the loading queue
    /// </summary>
    class JGenerateChunks : LevelQueueManagerJob {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JGenerateChunks(Level<VoxelStorageType> level) : base(level) {
        threadName = "Generate Chunk Manager";
      }

      /// <summary>
      /// Get the correct child job
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <param name="parentCancellationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JGenerateChunkColumn(this, chunkColumnLocation);
      }

      /// <summary>
      /// A Job for generating a new column of chunks into a level
      /// </summary>
      class JGenerateChunkColumn : ChunkColumnWorkerJob {

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkColumnLocation"></param>
        /// <param name="parentCancellationSources"></param>
        internal JGenerateChunkColumn(
          JGenerateChunks jobManager,
          Coordinate chunkColumnLocation
        ) : base(jobManager, chunkColumnLocation) {
          threadName = "Generate Column: " + chunkColumnLocation;
        }

        /// <summary>
        /// Threaded function, loads all the voxel data for this chunk
        /// </summary>
        protected override void doWorkOnChunk(Coordinate chunkLocation) {
          if (jobManager.level.getChunk(chunkLocation).isEmpty) {
            VoxelStorageType voxelData = jobManager.level.generateVoxelDataForChunk(chunkLocation);
            jobManager.level.setChunkData(chunkLocation, voxelData);
            World.NotifyAllOf(new ChunkDataLoadingFinishedEvent(chunkLocation));
          }
        }
      }
    }

    /// <summary>
    /// A job to un-load and serialize all chunks from the unloading queue
    /// </summary>
    class JUnloadChunks : LevelQueueManagerJob {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JUnloadChunks(Level<VoxelStorageType> level) : base(level) {
        threadName = "Unload Chunk Manager";
      }

      /// <summary>
      /// Get the child job
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <param name="parentCancellationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JUnloadChunkColumn(this, chunkColumnLocation);
      }

      /// <summary>
      /// A Job for un-loading the data for a column of chunks into a serialized file
      /// </summary>
      class JUnloadChunkColumn : ChunkColumnWorkerJob {

        /// <summary>
        /// Make a new job
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkColumnLocation"></param>
        /// <param name="resourcePool"></param>
        internal JUnloadChunkColumn(
          JUnloadChunks jobManager,
          Coordinate chunkColumnLocation
        ) : base(jobManager, chunkColumnLocation) {
          threadName = "Unload Column: " + queueItem.ToString();
        }

        /// <summary>
        /// Threaded function, serializes this chunks voxel data and removes it from the level
        /// </summary>
        protected override void doWorkOnChunk(Coordinate chunkLocation) {
          jobManager.level.saveChunkDataToFile(chunkLocation);
          jobManager.level.removeChunk(chunkLocation);
          World.NotifyAllOf(new ChunkDataUnloadingFinishedEvent(chunkLocation));
        }
      }
    }

    /// <summary>
    /// The job manager this manager uses
    /// </summary>
    class JGenerateChunkMeshes : LevelQueueManagerJob {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JGenerateChunkMeshes(Level<VoxelStorageType> level) : base(level) {
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
          if (!jobManager.level.containsChunkMesh(chunkLocation)) {
            IMesh mesh = jobManager.level.generateMeshDataForChunk(chunkLocation);
            if (!mesh.isEmpty && !jobManager.level.containsChunkMesh(chunkLocation)) {
              jobManager.level.setChunkMesh(chunkLocation, mesh);
              World.NotifyAllOf(new ChunkMeshReadyForRenderEvent(chunkLocation));
            }
          }
        }
      }
    }

    /// <summary>
    /// A base job for managing chunk work queues
    /// </summary>
    public abstract class LevelQueueManagerJob : QueueManagerJob<Coordinate> {

      /// <summary>
      /// The level we're loading for
      /// </summary>
      public Level<VoxelStorageType> level {
        get;
        protected set;
      }

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      protected LevelQueueManagerJob(Level<VoxelStorageType> level) : base(MaxChunkLoadingJobsCount) {
        this.level = level;
      }

      /// <summary>
      /// Base class for child jobs that manage chunk loading and unloading
      /// </summary>
      protected abstract class ChunkColumnWorkerJob : QueueTaskChildJob<Coordinate> {

        /// <summary>
        /// Job managing this job
        /// </summary>
        protected new LevelQueueManagerJob jobManager;

        /// <summary>
        /// Make a new job
        /// </summary>
        protected ChunkColumnWorkerJob(
          LevelQueueManagerJob jobManager,
          Coordinate chunkColumnLocation
        ) : base(chunkColumnLocation, jobManager) {
          this.jobManager = jobManager;
        }

        /// <summary>
        /// Do the actual work on the given chunk for this type of job
        /// </summary>
        protected abstract void doWorkOnChunk(Coordinate chunkLocation);

        /// <summary>
        /// Do work
        /// </summary>
        protected override void doWork(Coordinate chunkColumnLocation) {
          Coordinate columnTop = (chunkColumnLocation.x + 1, jobManager.level.chunkBounds.y, chunkColumnLocation.z + 1);
          Coordinate columnBottom = (chunkColumnLocation.x, 0, chunkColumnLocation.z);
          columnBottom.until(columnTop, chunkLocation => {
            doWorkOnChunk(chunkLocation);
          });
        }
      }
    }
  }
}