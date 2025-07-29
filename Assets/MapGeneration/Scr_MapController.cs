using Scellecs.Morpeh;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public sealed class Scr_MapController : MonoBehaviour {

    public World nodeWorld;

    public SystemsGroup systemsGroup;

    public Stash<ComponentMapNodeId> nodeIdStash;
    public Stash<ComponentMapNodePosition> nodePosStash;
    public Stash<ComponentMapNodeNeighbours> nodeNeighbStash;



    // collumn count does not include start and end nodes, only path in between
    private byte collumn_count = 9;
    // row count represents maximum POSSIBLE amount of rows, but will try to be belowe that point
    private byte row_count = 4;
    // map offset is the coordinate offset from screen borders on both sides of screen, from left to start and from right to end
    private int map_offset = 140;

    public void Start()
    {
        Debug.Log("MapController is Starting");

        //nodeWorld = World.Default;
        nodeWorld = World.Create();

        nodeWorld.UpdateByUnity = false;



        //var newEntity = newWorld.CreateEntity();
        //newWorld.RemoveEntity(newEntity);

        var newSystem = new SystemMapNodeDraw();

        //Debug.Log("made system");
        //newSystem.World = nodeWorld;
        //Debug.Log("changed world");

        systemsGroup = nodeWorld.CreateSystemsGroup();
        systemsGroup.AddSystem(newSystem);

        nodeWorld.AddSystemsGroup(order: 0, systemsGroup);
        //nodeWorld.RemoveSystemsGroup(systemsGroup);


        GenerateMap(collumn_count, row_count);
    }

    public void GenerateMap(byte collumns, byte rows)
    {
        Debug.Log("MapController is GeneratingMap");

        nodeIdStash = nodeWorld.GetStash<ComponentMapNodeId>();
        nodePosStash = nodeWorld.GetStash<ComponentMapNodePosition>();
        nodeNeighbStash = nodeWorld.GetStash<ComponentMapNodeNeighbours>();


        // ----------------------------------- First walkthrough - generate nodes and give IDs 
        // generation of first node, without any offset
        var entityFirst = nodeWorld.CreateEntity();
        nodeIdStash.Set(entityFirst, new ComponentMapNodeId { node_id = 0 });
        nodePosStash.Set(entityFirst, new ComponentMapNodePosition { node_x = map_offset, node_y = 540});

        byte temp_node_count = 1;

        for (int i = 0; i < collumns; i++)
        {
            var entity = nodeWorld.CreateEntity();
            var temp_x = (((1920 - (map_offset) * 2) / collumns) * temp_node_count);
            var temp_y = 540;

            nodeIdStash.Set(entity, new ComponentMapNodeId { node_id = temp_node_count });
            nodePosStash.Set(entity, new ComponentMapNodePosition { node_x = temp_x, node_y = temp_y });

            temp_node_count++;
        }

        // generation of last node, without any offset
        var entityLast = nodeWorld.CreateEntity();
        nodeIdStash.Set(entityLast, new ComponentMapNodeId { node_id = temp_node_count });
        nodePosStash.Set(entityLast, new ComponentMapNodePosition { node_x = 1920 - map_offset, node_y = 540 });

        MapUpdate();
        return;

        // ----------------------------------- Second walkthrough - create connections between nodes and give propper offset
        nodeNeighbStash.Set(entityFirst, new ComponentMapNodeNeighbours { });



        // ----------------------------------- Third walkthrough - give specific types of events to all nodes





        MapUpdate();
    }

    public void MapUpdate()
    {
        Debug.Log("MapController is Updating");

        //manually world updates
        nodeWorld.Update(Time.deltaTime);

        //apply all entity changes, filters will be updated.
        //automatically invoked between systems
        nodeWorld.Commit();

    }

}
