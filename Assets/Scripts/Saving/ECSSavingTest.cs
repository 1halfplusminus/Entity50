using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Transforms;
using UnityEngine;

namespace RPG.Saving {

    public partial struct ECSSavingTest : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            
        }

        public void OnDestroy(ref SystemState state)
        {
        
        }
 
        public void OnUpdate(ref SystemState state)
        {
        
            // var query = state.GetEntityQuery(ComponentType.ReadOnly<Saveable>());
            // var chunks = query.CreateArchetypeChunkArray(Allocator.Temp);
            // for(int i = 0; i < chunks.Length; i++){
            //     var chunk = chunks[i];
            //     var componentTypes = chunk.Archetype.GetComponentTypes(Allocator.Temp);
            //     for(int j = 0; j < componentTypes.Length; j++)
            //     {   
            //         var componentType = componentTypes[j];
            //         var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
            //         if(!componentType.IsZeroSized){
            //             var components = chunk.GetDynamicComponentDataArrayReinterpret<byte>(state.GetDynamicComponentTypeHandle(componentType),typeInfo.ElementSize);
            //             Debug.Log($"component saved: {components.Length}");
            //         }
                 
            //         // writer.WriteArray(components);
            //         // components.Dispose();
            //     }
            //     // componentTypes.Dispose();
            // }

            // chunks.Dispose();
        }
    }
}