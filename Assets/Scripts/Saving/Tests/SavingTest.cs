
using NUnit.Framework;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace RPG.Test
{
    struct ChunkComponents {
        public ulong Type;
        public BlobArray<byte> Values;
    }

    struct SerializableChunk
    {
        public BlobArray<Entity> Entities;
        public BlobArray<ChunkComponents> Components;

    }
    struct Save
    {
        public BlobArray<int> Data;
    }
    struct TestSave : IComponentData
    {
        public BlobArray<SerializableChunk> Data;
    }
    struct SavedComponent : IComponentData
    {
        public int Value;
    }
    public class SavingTest
    {

        [Test]
        public void TestSavingNativeArray()
        {
            // using var writer = new MemoryBinaryWriter();
            var path = Application.streamingAssetsPath + "/test.raw";
            var test = new NativeArray<int>(1, Allocator.Temp);
            test[0] = 1;
            using var blobBuilder = new BlobBuilder(Allocator.Persistent);
            ref var save = ref blobBuilder.ConstructRoot<Save>();
            var arrayBuilder = blobBuilder.Allocate(ref save.Data, test.Length);
            unsafe
            {
                UnsafeUtility
                .MemCpy(arrayBuilder.GetUnsafePtr(), test.GetUnsafePtr(), test.Length);
            }
            // using var blobRef = blobBuilder.CreateBlobAssetReference<Save>(Allocator.Temp);
            BlobAssetReference<Save>.Write(blobBuilder, path, 1);
            test.Dispose();
            // writer.WriteArray(test);
            // using var stream = File.Open(Application.streamingAssetsPath + "/test.raw", FileMode.OpenOrCreate);
            // var streamWriter = new System.IO.BinaryWriter(stream);
            // unsafe
            // {
            //     var ptr = test.GetUnsafePtr();
            //     for (int i = 0; i < writer.Length; i++)
            //     {
            //         streamWriter.Write(UnsafeUtility.ReadArrayElement<byte>(ptr, i));
            //     }
            // }
            // test.Dispose();
        }
                [Test]
        public void TestReadingNativeArray()
        {
            var path = Application.streamingAssetsPath + "/test.raw";
            BlobAssetReference<Save>.TryRead(path, 1, out var result);
            if (result.IsCreated)
            {
                for (int i = 0; i < result.Value.Data.Length; i++)
                {
                    Debug.Log($"{result.Value.Data[i]}");
                }
            }
        }
        [Test]
        public void TestSaving()
        {
            var path = Application.streamingAssetsPath + "/save.raw";
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            // Create test entity
            var entity = em.CreateEntity();
            em.AddComponents(entity, new ComponentTypes(new ComponentType[] { typeof(SavedComponent) }));
            em.SetComponentData(entity,new SavedComponent{Value = 10});
            // Get chuncks
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SavedComponent>());
            var chunks = query.CreateArchetypeChunkArray(Allocator.Temp);
            // Create Saveable blob asset
            using var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var save = ref blobBuilder.ConstructRoot<TestSave>();
            var chuncksBuilder = blobBuilder.Allocate(ref save.Data,chunks.Length);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var componentTypes = chunk.Archetype.GetComponentTypes(Allocator.Temp);
                var entities = chunk.GetNativeArray(em.GetEntityTypeHandle());
                // create serializedChunck
                unsafe{
                    chuncksBuilder[i] =  new SerializableChunk{};
                    var entitiesBuilder = blobBuilder.Allocate(ref chuncksBuilder[i].Entities, chunk.ChunkEntityCount);
                    UnsafeUtility.MemCpy(entitiesBuilder.GetUnsafePtr(), entities.GetUnsafeReadOnlyPtr(),chunk.ChunkEntityCount);
                    var chunkComponentsBuilder = blobBuilder.Allocate(ref chuncksBuilder[i].Components,componentTypes.Length );
                    for (int j = 0; j < componentTypes.Length; j++)
                    {

                        var componentType = componentTypes[j];
                        var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
                        int elementSize = typeInfo.ElementSize;
                        chunkComponentsBuilder[j].Type = typeInfo.StableTypeHash;
                        if (!componentType.IsZeroSized)
                        {
                            DynamicComponentTypeHandle chunkComponentType = em.GetDynamicComponentTypeHandle(componentType);

                            var components = chunk.GetDynamicComponentDataArrayReinterpret<byte>(chunkComponentType, elementSize);
                            var componentsBuilder = blobBuilder.Allocate(ref chunkComponentsBuilder[j].Values, components.Length);
                            UnsafeUtility.MemCpy(componentsBuilder.GetUnsafePtr(),components.GetUnsafePtr(), components.Length);
                        }

                        // writer.WriteArray(components);
                        // components.Dispose();
                    }
                }
                
                // componentTypes.Dispose();
            }
            BlobAssetReference<TestSave>.Write(blobBuilder,path,1);
            chunks.Dispose();
        }
        [Test]
        public void TestReadSave()
        {
            var path = Application.streamingAssetsPath + "/save.raw";
            BlobAssetReference<TestSave>.TryRead(path,1,out var result);
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            var breakEntity = defaultWorld.EntityManager.CreateEntity();
            defaultWorld.EntityManager.AddComponentData(breakEntity, new SavedComponent{Value = 2});
            // var entityRemap = defaultWord.EntityManager.CreateEntityRemapArray(Allocator.Temp);
            if(result.IsCreated){
                ref var save = ref result.Value;
                for(int i =0 ; i < save.Data.Length; i++){
                    Debug.Log($"Found Entity {save.Data[i].Entities[0].Index}");
                    var e = defaultWorld.EntityManager.CreateEntity();
                    for(var j = 0; j < save.Data[i].Components.Length; j++){
                        ref var chunkComponent = ref save.Data[i].Components[j];
                        var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(chunkComponent.Type);
                        var componentType = TypeManager.GetType(typeIndex);
                        var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                        defaultWorld.EntityManager.AddComponent(e, componentType);
                        var dynamicComponentTypeHandle = defaultWorld.EntityManager.GetDynamicComponentTypeHandle(componentType);
                        var chunk = defaultWorld.EntityManager.GetChunk(e);
                        var components = chunk.GetDynamicComponentDataArrayReinterpret<byte>(dynamicComponentTypeHandle,typeInfo.ElementSize);
                        unsafe{
                            var start = (chunk.ChunkEntityCount - save.Data[i].Entities.Length) * typeInfo.ElementSize;
                            UnsafeUtility.MemCpy(components.Slice(start).GetUnsafePtr(),chunkComponent.Values.GetUnsafePtr(),chunkComponent.Values.Length);
                        }
                        Debug.Log($"{componentType.Name}");
                    }
                    var savedComponent = defaultWorld.EntityManager.GetComponentData<SavedComponent>(e);
                    Debug.Log($" Saved component : {savedComponent.Value}");
                }
            }
            var existingComponent = defaultWorld.EntityManager.GetComponentData<SavedComponent>(breakEntity);
            Debug.Log($"Existing component : {existingComponent.Value}");
        }   
    }

}
