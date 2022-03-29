


using UnityEngine;
public class BoneAuthoring : MonoBehaviour {
    public Transform Transform;
}
namespace Test {
 public class MonoBehaviourComponentConversionSystem : GameObjectConversionSystem
        {
            protected override void OnUpdate()
            {

              this.AddTypeToCompanionWhiteList(typeof(Animator));
              this.AddTypeToCompanionWhiteList(typeof(BoneAuthoring));
              // Entities.ForEach((Animator animator)=>{
              //   DstEntityManager.AddComponentObject(GetPrimaryEntity(animator),animator);
              // });
              // Entities.ForEach((Transform transform)=>{
              //   var animator = transform.gameObject.GetComponentInParent<Animator>();
              // });

            }
        }
}
