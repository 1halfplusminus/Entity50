

using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering.Universal;


// public class TestAuthoring : MonoBehaviour, IConvertGameObjectToEntity
// {
//     public Entity Parent;
//     public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
//     {
//         var animator = GetComponentInChildren<Animator>();
//         var renderer = GetComponentInChildren<SkinnedMeshRenderer>();
//         dstManager.AddComponentObject(entity, animator);

//         var skeleton = renderer.bones;
//         for(int i = 0; i < skeleton.Length; i++) {
//             var boneEntity = conversionSystem.GetPrimaryEntity(skeleton[i]);
//             dstManager.AddComponentObject(boneEntity,skeleton[i]);
//         }
//         if(Parent != Entity.Null){
//             dstManager.AddComponentData(entity,new Parent{Value = Parent});
//         }
//     }
// }
// public struct NotInstancied : IComponentData {
   
// }
// [ConverterVersion("unity", 1)]
// public  class DoohickeyConversionSystem : GameObjectConversionSystem
// {

//     protected override void OnUpdate()
//     {
        
//         Entities.ForEach((Doohickey doohickey) =>
//         {
//             var entity = GetPrimaryEntity(doohickey);
//             DstEntityManager.AddComponentObject(entity, doohickey);
//             DstEntityManager.AddComponent<NotInstancied>(entity);
        
//         });
//     }
// }
// [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
// public class InstantiateAnimatorConversionSystem : GameObjectConversionSystem
// {
//     protected override void OnUpdate()
//     {
//         Entities.ForEach((Doohickey animator) =>
//         {
//             var entity = GetPrimaryEntity(animator);
//              if(DstEntityManager.HasComponent<Prefab>(entity)){
//                 Debug.Log("Here");
//                 var instance = Object.Instantiate(animator);
//                 DstEntityManager.AddComponentObject(entity,instance);
//                 DstEntityManager.AddComponentObject(entity,instance.transform);
//                 DstEntityManager.AddComponent<CopyTransformToGameObject>(entity);
//             } 
//         });
//     }
// }
// public partial class TestAnimator : SystemBase
// {
//     protected override void OnUpdate()
//     {
//         // Entities.WithAll<NotInstancied>().ForEach((Entity e,Animator animator) =>{
//         //     var instance = Object.Instantiate(animator);
//         //     EntityManager.AddComponentObject(e,instance);
//         //     EntityManager.AddComponentObject(e,instance.transform);
//         //     EntityManager.AddComponent<CopyTransformToGameObject>(e);
//         //     EntityManager.RemoveComponent<NotInstancied>(e);

//         //     Object.Destroy(instance.GetComponentInChildren<SkinnedMeshRenderer>().gameObject);
//         // }).WithStructuralChanges().Run();
//         Entities.ForEach((Entity e,Animator animator) => {
//             animator.Update(Time.DeltaTime);
//         }).WithStructuralChanges().Run();
//     }
// }