using MeepTech.Events;
using MeepTech.GamingBasics;
using MeepTech.Voxel.Collections.Level;
using MeepTech.Voxel.Collections.Storage;
using MeepTech.Voxel.Generation.Sources;
using System;

namespace MeepTech.Voxel.Generation.Managers {

  /// <summary>
  /// Job based chunk voxel generation
  /// </summary>
  /// <typeparam name="VoxelStorageType"></typeparam>
  class JobBasedChunkVoxelDataGenManager<VoxelStorageType> : ChunkVoxelDataGenerationManager<VoxelStorageType>
    where VoxelStorageType : VoxelStorage {

    /// <summary>
    /// The job used to generate chunks
    /// </summary>
    JGenerateChunks chunkGenerationJobManager;

    /// <summary>
    /// Construct
    /// </summary>
    /// <param name="level">The level this manager is managing for</param>
    public JobBasedChunkVoxelDataGenManager(ILevel level, IChunkDataStorage chunkDataStorage, IVoxelSource voxelSource) : base(level, chunkDataStorage, voxelSource) {
      chunkGenerationJobManager = new JGenerateChunks(level, this);
    }

    /// <summary>
    /// Listen for events
    /// </summary>
    /// <param name="event"></param>
    /// <param name="origin"></param>
    public override void notifyOf(IEvent @event, IObserver origin = null) {
      switch(@event) {
        // if chunk data wasn't found in a file, lets generate it for them
        case ChunkFileDataLoadingManager<VoxelFlatArray>.ChunkDataNotFoundInFilesEvent cfdlmcdnfife:
          chunkGenerationJobManager.enQueue(new Coordinate[] { cfdlmcdnfife.chunkLocation });
          break;
        default:
          return;
      }
    }

    /// <summary>
    /// A job to load all chunks from the loading queue
    /// </summary>
    class JGenerateChunks : ChunkQueueManagerJob<JobBasedChunkVoxelDataGenManager<VoxelStorageType>> {

      /// <summary>
      /// Create a new job, linked to the level
      /// </summary>
      /// <param name="level"></param>
      public JGenerateChunks(ILevel level, JobBasedChunkVoxelDataGenManager<VoxelStorageType> manager) : base(level, manager) {
        threadName = "Generate Chunk Manager";
      }

      /// <summary>
      /// Get the correct child job
      /// </summary>
      /// <param name="chunkColumnLocation"></param>
      /// <param name="parentCancellationSources"></param>
      /// <returns></returns>
      protected override QueueTaskChildJob<Coordinate> getChildJob(Coordinate chunkColumnLocation) {
        return new JGenerateChunk(this, chunkColumnLocation);
      }

      /// <summary>
      /// A Job for generating a new column of chunks into a level
      /// </summary>
      class JGenerateChunk : QueueTaskChildJob<Coordinate> {

        /// <summary>
        /// The level we're loading for
        /// </summary>
        protected new JGenerateChunks jobManager;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkLocation"></param>
        /// <param name="parentCancellationSources"></param>
        internal JGenerateChunk(
          JGenerateChunks jobManager,
          Coordinate chunkLocation
        ) : base(chunkLocation, jobManager) {
          this.jobManager = jobManager;
          threadName = "Generating voxels for chunk: " + chunkLocation;
        }

        /// <summary>
        /// Threaded function, loads all the voxel data for this chunk
        /// </summary>
        protected override void doWork(Coordinate chunkLocation) {
          // if the chunk is empty, lets try to fill it.
          if (jobManager.level.getChunk(chunkLocation).isEmpty) {
            VoxelStorageType voxelData = jobManager.manager.generateVoxelDataForChunk(chunkLocation);
            jobManager.manager.chunkDataStorage.setChunkVoxelData(chunkLocation, voxelData);
            World.EventSystem.notifyChannelOf(
              new ChunkFileDataLoadingManager<VoxelStorageType>.ChunkDataLoadingFinishedEvent(chunkLocation),
              Evix.EventSystems.WorldEventSystem.Channels.TerrainGeneration
            );
          } else {
            World.Debugger.log($"Tried to generate the voxels for a non-empty chunk: {chunkLocation.ToString()}");
          }
        }
      }
    }
  }
}
