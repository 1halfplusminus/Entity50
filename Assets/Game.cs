
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine.InputSystem;
public struct GoInGameRequest : IRpcCommand {
    
}

public struct CubeInput : ICommandData
{
    public uint Tick {get; set;}
    public int horizontal;
    public int vertical;
}

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public partial class SampleCubeInput : SystemBase{

    ClientSimulationSystemGroup clientSimulationSystemGroup;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<NetworkIdComponent>();
        clientSimulationSystemGroup = World.GetExistingSystem<ClientSimulationSystemGroup>();
    }

    protected override void OnUpdate()
    {
        var localInput = GetSingleton<CommandTargetComponent>().targetEntity;
        if(localInput  == Entity.Null){
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
            var commandTargetEntity = GetSingletonEntity<CommandTargetComponent>();
            Entities
            .WithAll<MouvableCubeComponent>()
            .WithNone<CubeInput>().ForEach((Entity ent, in GhostOwnerComponent ghostOwner)=>{
                if(ghostOwner.NetworkId == localPlayerId){
                    commandBuffer.AddBuffer<CubeInput>(ent);
                    commandBuffer.SetComponent(commandTargetEntity, new CommandTargetComponent{targetEntity = ent});
                }
            }).Run();
            commandBuffer.Playback(EntityManager);
            return;
        }
        var input = default(CubeInput);
        input.Tick = clientSimulationSystemGroup.ServerTick;
        if(Keyboard.current.aKey.isPressed){
            input.horizontal -= 1;
        }
        if(Keyboard.current.dKey.isPressed){
            input.horizontal += 1;
        }
        if(Keyboard.current.sKey.isPressed){
            input.vertical -= 1;
        }
        if(Keyboard.current.vKey.isPressed){
            input.vertical += 1;
        }
        var inputBuffer = EntityManager.GetBuffer<CubeInput>(localInput);
        inputBuffer.AddCommandData(input);
    }
}

[UpdateInGroup(typeof(GhostPredictionSystemGroup))]
public partial class MoveCubeSystem : SystemBase
{
    GhostPredictionSystemGroup ghostPredictionSystemGroup;
    protected override void OnCreate(){ 
        base.OnCreate();
        ghostPredictionSystemGroup = World.GetExistingSystem<GhostPredictionSystemGroup>();
    }
    protected override void OnUpdate()
    {
        var tick = ghostPredictionSystemGroup.PredictingTick;
        var deltaTime = Time.DeltaTime;
        Entities
        .ForEach((DynamicBuffer<CubeInput> inputBuffer,ref Translation trans,in PredictedGhostComponent predicted)=>{
            if(!GhostPredictionSystemGroup.ShouldPredict(tick,predicted)){
                return;
            }
            inputBuffer.GetDataAtTick(tick, out CubeInput input);
            if (input.horizontal > 0)
                trans.Value.x += deltaTime;
            if (input.horizontal < 0)
                trans.Value.x -= deltaTime;
            if (input.vertical > 0)
                trans.Value.z += deltaTime;
            if (input.vertical < 0)
                trans.Value.z -= deltaTime;
        }).ScheduleParallel();
    }
}
[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public partial class GoInGameClientSystem : SystemBase
{
    protected override void OnCreate(){
        RequireSingletonForUpdate<CubeSpawner>();
        RequireForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<NetworkIdComponent>(),ComponentType.Exclude<NetworkStreamInGame>())
        );
    }
    protected override void OnUpdate()
    {
      var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

      Entities.WithNone<NetworkStreamInGame>().ForEach((Entity ent,in NetworkIdComponent id)=>{
          commandBuffer.AddComponent<NetworkStreamInGame>(ent);
          var req = commandBuffer.CreateEntity();
          commandBuffer.AddComponent<GoInGameRequest>(req);
          commandBuffer.AddComponent(req,new SendRpcCommandRequestComponent{TargetConnection = ent});
      }).Run();

      commandBuffer.Playback(EntityManager);
    }
}
[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public partial class GoInGameServerSystem : SystemBase
{
    protected override void OnCreate(){
        RequireSingletonForUpdate<CubeSpawner>();
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<GoInGameRequest>(),ComponentType.ReadOnly<ReceiveRpcCommandRequestComponent>()));
    }
    protected override void OnUpdate()
    {
        var prefab = GetSingleton<CubeSpawner>().Cube;
        var networkIdFromEntity = GetComponentDataFromEntity<NetworkIdComponent>(true);
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        Entities.WithNone<SendRpcCommandRequestComponent>()
        .ForEach((Entity reqEnt, in GoInGameRequest req, in ReceiveRpcCommandRequestComponent reqSrc)=>{
            commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
            UnityEngine.Debug.Log($"Server setting connection { networkIdFromEntity[reqSrc.SourceConnection].Value} to in game");
            var player = commandBuffer.Instantiate(prefab);
            commandBuffer.SetComponent(player, 
                new GhostOwnerComponent{
                    NetworkId = networkIdFromEntity[reqSrc.SourceConnection].Value}
            );
            commandBuffer.AddBuffer<CubeInput>(player);
            commandBuffer.SetComponent(reqSrc.SourceConnection,new CommandTargetComponent{targetEntity = player});

            commandBuffer.DestroyEntity(reqEnt);
        }).Run();

        commandBuffer.Playback(EntityManager);
    }
}