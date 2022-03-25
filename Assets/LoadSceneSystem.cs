// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Jobs;
// using Unity.Mathematics;
// using Unity.Scenes;
// using Unity.Transforms;
// using UnityEngine.SceneManagement;
// using UnityEngine;
// public partial class LoadSceneSystem : SystemBase
// {
//     protected bool Loaded;

//     SceneSystem sceneSystem;

//     protected override void OnCreate(){
//          sceneSystem = World.GetExistingSystem<SceneSystem>();
//     }
//     protected override void OnUpdate()
//     {

//         if(Loaded == false){
//             // var scene = SceneManager.GetSceneAt(1);
//             var hash = new UnityEngine.Hash128();
//             hash.Append("ac873ceb49f9a0f29e417bc1f1c27657");
//             sceneSystem.LoadSceneAsync(hash, new SceneSystem.LoadParameters{ Flags = SceneLoadFlags.LoadAsGOScene});
//             Loaded = true;
//         }
//     }
// }
