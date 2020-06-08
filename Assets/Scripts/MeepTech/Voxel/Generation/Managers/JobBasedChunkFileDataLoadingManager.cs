﻿using MeepTech.Voxel.Collections.Level;
using MeepTech.Voxel.Collections.Storage;
using MeepTech.Jobs;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using MeepTech.GamingBasics;
using MeepTech.Events;
using Evix;
using Evix.EventSystems;
using System.Threading;

namespace MeepTech.Voxel.Generation.Managers {

  /// <summary>
  /// A manager (message handler + doer) for loading chunks fom files.
  /// </summary>
  /// <typeparam name="VoxelStorageType"></typeparam>
  public class JobBasedChunkFileDataLoadingManager<VoxelStorageType> : ChunkFileDataLoadingManager<VoxelStorageType>
    where VoxelStorageType : IVoxelStorage {

    /// <summary>
    /// The current parent job, in charge of loading the chunks in the load queue
    /// </summary>
    JLoadChunks chunkLoadQueueManagerJob;

    /// <summary>
    /// The current parent job, in charge of loading the chunks in the load queue
    /// </summary>
    JUnloadChunks chunkUnloadQueueManagerJob;

    /// <summary>
    /// construct
    /// </summary>
    public JobBasedChunkFileDataLoadingManager(ILevel level, IChunkDataStorage chunkDataStorage) : base (level, chunkDataStorage) {
      chunkLoadQueueManagerJob = new JLoadChunks(level, this);
      chunkUnloadQueueManagerJob = new JUnloadChunks(level, this);
    }

    /// <summary>
    /// Add a list of chunks that we want to load from file
    /// </summary>
    /// <param name="chunkLocations"></param>
    public override void addChunksToLoad(Coordinate[] chunkLocations) {
      new Thread(() => {
        chunkLoadQueueManagerJob.enQueue(chunkLocations);
        chunkUnloadQueueManagerJob.deQueue(chunkLocations);
      }) { Name = "Add Chunks To File Loading Queue" }.Start();
    }

    /// <summary>
    /// Adda list of chunks we want to unload to file storage
    /// </summary>
    /// <param name="chunkLocations"></param>
    public override void addChunksToUnload(Coordinate[] chunkLocations) {
      new Thread(() => {
        chunkUnloadQueueManagerJob.enQueue(chunkLocations);
        chunkLoadQueueManagerJob.deQueue(chunkLocations);
      }) { Name = "Add Chunks To File Un-Loading Queue" }.Start();
    }

    public override void notifyOf(IEvent @event, IObserver origin = null) {
      return;
    }

    /// <summary>
    /// A job to load all chunks from the loading queue
    /// </summary>
    class JLoadChunks : ChunkQueueManagerJob<JobBasedChunkFileDataLoadingManager<VoxelStorageType>> {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></pa
      public JLoadChunks(ILevel level, JobBasedChunkFileDataLoadingManager<VoxelStorageType> manager) : base(level, manager) {
        threadName = "Load Chunk Manager";
      }

      /// <summary>
      /// Get the correct child job
      /// </summary>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JLoadChunksFromFile(this, chunkColumnLocation);
      }

      /// <summary>
      /// Override to shift generational items to their own queue
      /// </summary>
      /// <param name="chunkLocation"></param>
      /// <returns></returns>
      protected override bool isAValidQueueItem(Coordinate chunkLocation) {
        // if this doesn't have a loaded file, remove it from this queue and load it in the generation one
        if (!File.Exists((manager as JobBasedChunkFileDataLoadingManager<VoxelStorageType>).getChunkFileName(chunkLocation))) {
          return false;
        }

        return base.isAValidQueueItem(chunkLocation);
      }

      /// <summary>
      /// Do something on an invalid item before we toss it out
      /// </summary>
      /// <param name="chunkLocation"></param>
      protected override void onQueueItemInvalid(Coordinate chunkLocation) {
        World.EventSystem.notifyChannelOf(
          new ChunkDataNotFoundInFilesEvent(chunkLocation),
          WorldEventSystem.Channels.TerrainGeneration
        );
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
      class JLoadChunksFromFile : QueueTaskChildJob<Coordinate> {

        /// <summary>
        /// The level we're loading for
        /// </summary>
        protected new JLoadChunks jobManager;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkColumnLocation"></param>
        /// <param name="parentCancellationSources"></param>
        internal JLoadChunksFromFile(
          JLoadChunks jobManager,
          Coordinate chunkColumnLocation
        ) : base(chunkColumnLocation, jobManager) {
          this.jobManager = jobManager;
          threadName = "Load Column: " + chunkColumnLocation;
        }

        /// <summary>
        /// Threaded function, loads all the voxel data for this chunk
        /// </summary>
        protected override void doWork(Coordinate chunkLocation) {
          if (jobManager.level.getChunk(chunkLocation).isEmpty) {
            VoxelStorageType voxelData = jobManager.manager.getVoxelDataForChunkFromFile(chunkLocation);
            jobManager.manager.chunkDataStorage.setChunkVoxelData(chunkLocation, voxelData);
            World.EventSystem.notifyChannelOf(
              new ChunkDataLoadingFinishedEvent(chunkLocation),
              WorldEventSystem.Channels.TerrainGeneration
            );
          } else {
            World.Debugger.log($"Tried to generate the voxels for a non-empty chunk: {chunkLocation.ToString()}");
          }
        }
      }
    }

    /// <summary>
    /// A job to un-load and serialize all chunks from the unloading queue
    /// </summary>
    class JUnloadChunks : ChunkQueueManagerJob<JobBasedChunkFileDataLoadingManager<VoxelStorageType>> {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JUnloadChunks(ILevel level, JobBasedChunkFileDataLoadingManager<VoxelStorageType> manager) : base(level, manager) {
        threadName = "Unload Chunk Manager";
      }

      /// <summary>
      /// Get the child job
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <param name="parentCancellationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JUnloadChunkToFile(this, chunkColumnLocation);
      }

      /// <summary>
      /// A Job for un-loading the data for a column of chunks into a serialized file
      /// </summary>
      class JUnloadChunkToFile : QueueTaskChildJob<Coordinate> {

        /// <summary>
        /// The level we're loading for
        /// </summary>
        protected new JUnloadChunks jobManager;

        /// <summary>
        /// Make a new job
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkColumnLocation"></param>
        /// <param name="resourcePool"></param>
        internal JUnloadChunkToFile(
          JUnloadChunks jobManager,
          Coordinate chunkColumnLocation
        ) : base(chunkColumnLocation, jobManager) {
          this.jobManager = jobManager;
          threadName = "Unload chunk to file: " + queueItem.ToString();
        }

        /// <summary>
        /// Threaded function, serializes this chunks voxel data and removes it from the level
        /// </summary>
        protected override void doWork(Coordinate chunkLocation) {
          jobManager.manager.saveChunkDataToFile(chunkLocation);
          jobManager.manager.chunkDataStorage.removeChunkVoxelData(chunkLocation);
          jobManager.manager.chunkDataStorage.removeChunkMesh(chunkLocation);
        }
      }
    }
  }
}
