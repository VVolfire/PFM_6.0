using Scellecs.Morpeh;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.RuleTile.TilingRuleOutput;
using Random = UnityEngine.Random;

public sealed class Scr_MapController : MonoBehaviour {

    public World nodeWorld;
    private Filter filterPos;

    public SystemsGroup systemsGroup;

    public Stash<MapNodeIdComponent> nodeIdStash;
    public Stash<MapNodePositionComponent> nodePosStash;
    public Stash<MapNodeNeighboursComponent> nodeNeighbStash;

    public GameObject nodePrefab;

    // collumn count does not include start and end nodes, only path in between
    private byte collumn_count = 9;
    // row count represents maximum POSSIBLE amount of rows, but will try to be belowe that point
    private byte row_count = 4;
    // map offset is the coordinate offset from screen borders on both sides of screen, from left to start and from right to end
    private int map_offset = 140;

    private int map_offset_start = 0;
    private int map_offset_end = 1920;

    public void Start()
    {
        Debug.Log("MapController is Starting");

        //nodeWorld = World.Default;
        nodeWorld = World.Create();

        nodeWorld.UpdateByUnity = false;



        //var newEntity = newWorld.CreateEntity();
        //newWorld.RemoveEntity(newEntity);

        var newSystem = new MapNodeDrawSystem();

        newSystem.myPrefab = nodePrefab;

        //Debug.Log("made system");
        //newSystem.World = nodeWorld;
        //Debug.Log("changed world");

        systemsGroup = nodeWorld.CreateSystemsGroup();
        systemsGroup.AddSystem(newSystem);

        nodeWorld.AddSystemsGroup(order: 0, systemsGroup);
        //nodeWorld.RemoveSystemsGroup(systemsGroup);

        this.filterPos = this.nodeWorld.Filter.With<MapNodePositionComponent>().Build();

        GenerateMap(collumn_count, row_count);
    }

    public void GenerateMap(byte collumns, byte rows)
    {
        Debug.Log("MapController is GeneratingMap");

        nodeIdStash = nodeWorld.GetStash<MapNodeIdComponent>();
        nodePosStash = nodeWorld.GetStash<MapNodePositionComponent>();
        nodeNeighbStash = nodeWorld.GetStash<MapNodeNeighboursComponent>();


        // ----------------------------------- First walkthrough - generate nodes and give IDs 
        Debug.LogWarning("----------------------------------- First walkthrough - generate nodes and give IDs");

        // generation of first node, without any offset
        var entityFirst = nodeWorld.CreateEntity();
        nodeIdStash.Set(entityFirst, new MapNodeIdComponent { node_id = 0 });
        nodePosStash.Set(entityFirst, new MapNodePositionComponent
        {
            node_x = map_offset_start + map_offset,
            node_y = 540,
            node_collumn = 0,
            node_row = rows/2
        });

        byte temp_node_count = 1;

        // temp array used for storing information about which rows are taken in past collumn.
        // Needed to create actually viable paths.
        byte[] temp_past_coll = new byte[rows];

        List<byte> temp_past_coll_copy = new List<byte>();

        // this is an initial loop that is used to fill past collumn with all rows. It is a crutch used to make 
        // filling first row after initial starting node possible with all combinations.
        for (byte i = 0; i < rows; i++)
        {
            temp_past_coll[i] = (byte)i;
        }

        for (byte i = 1; i <= collumns; i++)
        {
            Debug.Log(" ------------------------------------------------ Making Collumn - " + i);

            // rows + 1, since the end value is not inclusive and 2 at minimum, to avoid bottlenecks
            int rowsInColumn = Random.Range(2, rows + 1);
            Debug.Log(" will have this much rows = " + rowsInColumn);


            for (byte c = 0; c < rowsInColumn; c++)
            {
                Debug.Log(" ___ creating node with ID   " + temp_node_count);

                // create random row that we want to occupy, using past collumn rows
                byte temp_curr_row = RollForCurrentRow(temp_past_coll, rows);

                // check to see if there already are occupied position of same value
                bool temp_flag_march = CheckForListCollision(temp_past_coll_copy, temp_curr_row);

                // if it is, then we need to march to find the next best position
                if (temp_flag_march)
                {
                    var temp_buffer = MarchOnCollumn(temp_past_coll_copy, temp_curr_row, rows);
                    temp_curr_row = temp_buffer;
                }

                // after that, add this row to temp collumn copy values
                temp_past_coll_copy.Add(temp_curr_row);

                //bool temp_found_same_value = List.Exists(temp_past_coll_copy, p => p == temp_curr_row);
                //
                //if (temp_found_same_value)
                //{
                //    byte temp_curr_row_found_id = Array.Find(temp_past_coll_copy, p => p == temp_curr_row);
                //}
                //else
                //{
                //    temp_past_coll_copy;
                //}


                // create the entity and set initial values
                var entity = nodeWorld.CreateEntity();

                //var diff = (map_offset_end-map_offset)-(map_offset_start + map_offset);

                var temp_x = (int)(map_offset_start + map_offset + ((map_offset_end - map_offset*3) / collumns) * i);
                //var temp_x = (int)(map_offset_start + map_offset + (diff / collumns) * i);
                var temp_y = (int)((1080 / rows) * temp_curr_row);

                

                nodeIdStash.Set(entity, new MapNodeIdComponent { node_id = temp_node_count });

                nodePosStash.Set(entity, new MapNodePositionComponent { 
                    node_x = temp_x, 
                    node_y = temp_y,
                    node_collumn = i,
                    node_row = temp_curr_row
                });

                temp_node_count++;
            }

            string debug_list = string.Join(",", temp_past_coll_copy);
            Debug.Log("    !!!!!!!!!!!!!!!!!!!!!!!!    COLLUMN - " + i + " -   CREATED ROWS:  " + debug_list);


            Array.Clear(temp_past_coll,0, temp_past_coll.Length);

            temp_past_coll = temp_past_coll_copy.ToArray();

            temp_past_coll_copy.Clear();


        }

        // generation of last node, without any offset
        var entityLast = nodeWorld.CreateEntity();
        nodeIdStash.Set(entityLast, new MapNodeIdComponent { node_id = temp_node_count });
        nodePosStash.Set(entityLast, new MapNodePositionComponent
        {
            node_x = map_offset_end - map_offset,
            node_y = 540,
            node_collumn = collumns + 1,
            node_row = rows / 2
        });


        nodeWorld.Commit();


        // ----------------------------------- Second walkthrough - create connections between nodes and give propper offset
        Debug.LogWarning("----------------------------------- Second walkthrough - create connections between nodes and give propper offset");

        nodeNeighbStash.Set(entityFirst, new MapNodeNeighboursComponent { node_neighbours = new List<byte>() });

        List<Entity> prev_collumn_entities = new List<Entity>();
        List<Entity> current_collumn_entities = new List<Entity>();


        Debug.Log(".......................................GENERATING CLEAR CONNECTIONS.......................................");
        // FIRST WALKTHROUGH TO GENERATE CLEAR CONNECTIONS (on the same or adjacent row)
        // do a collumns + 1 to include the final end node
        for (byte i = 1; i <= collumns+1; i++)
        {
            Debug.Log($"---------- doing collumn number _{i}_ ----------");

            //prev_collumn_entities.Clear();
            prev_collumn_entities = current_collumn_entities;
            //current_collumn_entities.Clear();

            // need to fill current_collumn_entities list with entities of current collumn
            current_collumn_entities = SearchForEntitiesOfCollumn(i);

            // list to store neighbours that fit the row adjacency with current node

            // if this is the first collumn then all of the nodes have connections with previous 0 level that is the starting point
            if (i == 1)
            {
                Entity start_collumn_node_entity = SearchForEntitiesOfCollumn(0).First();

                // get past node id
                ref var nodePrevIdComponent = ref nodeIdStash.Get(start_collumn_node_entity);
                ref var nodePrevNeighbComponent = ref nodeNeighbStash.Get(start_collumn_node_entity);

                List<byte> neighbours = new List<byte>();

                // add past node id to neighbours of this node
                neighbours.Add(nodePrevIdComponent.node_id);

                Debug.Log($"---------- THIS IS FIRST COLLUMN ----------");
                foreach (var entity in current_collumn_entities)
                {
                    // get current node id
                    ref var nodeIdComponent = ref nodeIdStash.Get(entity);

                    // add current node id to neighbours of past node
                    //nodePrevNeighbComponent.node_neighbours.Add(nodeIdComponent.node_id);

                    //// add current node id to neighbours of past node
                    List<byte> add_neighbours = nodePrevNeighbComponent.node_neighbours;
                    add_neighbours.Add(nodeIdComponent.node_id);
                    nodePrevNeighbComponent.node_neighbours = add_neighbours;

                    // add current node id to neighbours of past node
                    //AddNeighbour(nodeIdComponent, nodePrevNeighbComponent);

                    nodeNeighbStash.Set(entity, new MapNodeNeighboursComponent { node_neighbours = neighbours });

                }
                continue;
            }

            // if this is the last collumn then there are only one node that has connections with full previous level since its the ending point
            if (i == collumns+1)
            {
                List<byte> neighbours = new List<byte>();
                Debug.LogWarning($"---------- THIS IS LAST COLLUMN ----------");
                foreach (var entity in current_collumn_entities)
                {
                    // get current node id
                    ref var nodeIdComponent = ref nodeIdStash.Get(entity);

                    foreach (var prev_entity in prev_collumn_entities)
                    {
                        // get past node id and neighb
                        ref var nodePrevIdComponent = ref nodeIdStash.Get(prev_entity);
                        ref var nodePrevNeighbComponent = ref nodeNeighbStash.Get(prev_entity);

                        // add current id to neighbours of past nodes
                        //nodePrevNeighbComponent.node_neighbours.Add(nodeIdComponent.node_id);

                        // add current node id to neighbours of past node
                        List<byte> add_neighbours = nodePrevNeighbComponent.node_neighbours;
                        add_neighbours.Add(nodeIdComponent.node_id);
                        nodePrevNeighbComponent.node_neighbours = add_neighbours;

                        // add current node id to neighbours of past node
                        //AddNeighbour(nodeIdComponent, nodePrevNeighbComponent);

                        // add past node id to neighbours of this node
                        neighbours.Add(nodePrevIdComponent.node_id);
                    }

                    nodeNeighbStash.Set(entity, new MapNodeNeighboursComponent { node_neighbours = neighbours });

                }
                continue;
            }

            foreach (var entity in current_collumn_entities)
            {
                List<byte> neighbours = new List<byte>();

                // make copy of prev collumn entities for quick way to find equals in row
                //List<byte> potential_clear_neighb = new List<byte>();
                List<Entity> potential_clear_neighb = new List<Entity>();

                // roll for max number of connections
                byte max_conn = (byte)Random.Range(1, 4);
                // get current pos component that has row position for current entity
                ref var nodeCurrPosComponent = ref nodePosStash.Get(entity);
                // get current node id
                ref var nodeCurrIdComponent = ref nodeIdStash.Get(entity);

                Debug.Log($"---\"---\"---\"--- making neighbours for node with id __{nodeCurrIdComponent.node_id}__");
                Debug.Log($"--- max connections is __{max_conn}__");


                foreach (var prev_entity in prev_collumn_entities)
                {
                    ref var nodePrevPosComponent = ref nodePosStash.Get(prev_entity);
                    ref var nodePrevIdComponent = ref nodeIdStash.Get(prev_entity);

                    Debug.Log($"--- prev entity id __{nodePrevIdComponent.node_id}__");
                    Debug.Log($"--- prev row = _{nodePrevPosComponent.node_row}_ und this row = _{nodeCurrPosComponent.node_row}_ ");
                    // check for if current row position is equal or adjacent to previous position
                    if ((nodeCurrPosComponent.node_row == nodePrevPosComponent.node_row)
                    || (nodeCurrPosComponent.node_row - 1 == nodePrevPosComponent.node_row)
                    || (nodeCurrPosComponent.node_row + 1 == nodePrevPosComponent.node_row))
                    {
                        Debug.Log("ALL GOOD, WILL ADD TO LIST");
                        //potential_clear_neighb.Add(nodePrevIdComponent.node_id);
                        potential_clear_neighb.Add(prev_entity);
                    }
                }
                Debug.Log($"--- found this much potential clear connections __{potential_clear_neighb.Count}__");

                for (byte c = 0; c < max_conn; c++)
                {
                    Debug.Log($"--- connection __{c+1}__");
                    if (potential_clear_neighb.Count > 0)
                    {
                        //var neighb_id = potential_clear_neighb[Random.Range(0, potential_clear_neighb.Count)];
                        var neighb_entity = potential_clear_neighb[Random.Range(0, potential_clear_neighb.Count)];

                        // get chosen prev neighb and id
                        ref var nodePrevNeighbComponent = ref nodeNeighbStash.Get(neighb_entity);
                        ref var nodePrevIdComponent = ref nodeIdStash.Get(neighb_entity);

                        Debug.Log($"--- rolled for and added id __{nodePrevIdComponent.node_id}__");

                        // add current id to neighbours of past nodes
                        //nodePrevNeighbComponent.node_neighbours.Add(nodeCurrIdComponent.node_id);

                        // add current node id to neighbours of past node
                        List<byte> add_neighbours = nodePrevNeighbComponent.node_neighbours;
                        add_neighbours.Add(nodeCurrIdComponent.node_id);
                        nodePrevNeighbComponent.node_neighbours = add_neighbours;

                        // add current node id to neighbours of past node
                        //AddNeighbour(nodeCurrIdComponent, nodePrevNeighbComponent);

                        // add past node id to neighbours of this node
                        //neighbours.Add(neighb_id);
                        //potential_clear_neighb.Remove(neighb_id);
                        neighbours.Add(nodePrevIdComponent.node_id);
                        potential_clear_neighb.Remove(neighb_entity);
                    }
                    else
                    {
                        Debug.Log($"---\"--- OUT OF POTENTIAL NEIGHBOURS ---\"---");
                        break;
                    }
                }

                nodeNeighbStash.Set(entity, new MapNodeNeighboursComponent { node_neighbours = neighbours });

            }
        }




        Debug.Log(".......................................GETTING RID OF DEAD ENDS AND GIVING OFFSET.......................................");
        // SECOND WALKTHROUGH TO GET RID OF DEAD ENDS AND GIVE OFFSET


        // pre loop preparations, look at first few lines with entities lists to understand this logic
        //current_collumn_entities.Clear();
        //prev_collumn_entities = SearchForEntitiesOfCollumn(0);
        //current_collumn_entities = SearchForEntitiesOfCollumn(0);
        List<Entity> next_collumn_entities = new List<Entity>();

        for (byte i = 1; i <= collumns; i++)
        {
            Debug.Log($"_______________ forcing connection on collumn {i} _______________");
            Debug.Log("_____ prev collumn _____");
            prev_collumn_entities = SearchForEntitiesOfCollumn((byte)(i - 1));

            Debug.Log("_____ curr collumn _____");
            // need to fill current_collumn_entities list with entities of current collumn
            current_collumn_entities = SearchForEntitiesOfCollumn(i);

            Debug.Log("_____ next collumn _____");
            // need to fill current_collumn_entities list with entities of current collumn
            next_collumn_entities = SearchForEntitiesOfCollumn((byte)(i + 1));



            // the code belowe has a weird quirk where found closest row neighbour is returned by its node_id
            // and then found again and used
            // It is done since returning row neighbour directly by Entity type does not allow for good check for emptiness, as in a returned entity if actually empty which means that there are no need to get forced connections
            foreach (var entity in current_collumn_entities)
            {
                // get current entity info
                ref var nodeCurrNeighbComponent = ref nodeNeighbStash.Get(entity);
                ref var nodeCurrIdComponent = ref nodeIdStash.Get(entity);

                // find best neighbour in previous collumn
                byte best_forced_id_prev = FindClosestRowNeighbour(entity, prev_collumn_entities, rows);
                if (best_forced_id_prev != 0)
                {
                    foreach (var ent in prev_collumn_entities)
                    {
                        ref var nodePotIdComponent = ref nodeIdStash.Get(ent);
                        if (nodePotIdComponent.node_id == best_forced_id_prev)
                        {
                            ref var nodePrevNeighbComponent = ref nodeNeighbStash.Get(ent);
                            ref var nodePrevIdComponent = ref nodeIdStash.Get(ent);

                            Debug.Log($"_!_!_!_!_!_ FOUND CORRECT ID FOR FORCED PREV CONNECTION FROM _{nodeCurrIdComponent.node_id}_ TO _{nodePrevIdComponent.node_id}_");

                            // add current node id to neighbours of prev node
                            List<byte> add_neighbours_prev = nodePrevNeighbComponent.node_neighbours;
                            add_neighbours_prev.Add(nodeCurrIdComponent.node_id);
                            nodePrevNeighbComponent.node_neighbours = add_neighbours_prev;

                            //AddNeighbour(nodeCurrIdComponent, nodePrevNeighbComponent);

                            // add prev node id to neighbours of current node
                            List<byte> add_neighbours_curr = nodeCurrNeighbComponent.node_neighbours;
                            add_neighbours_curr.Add(nodePrevIdComponent.node_id);
                            nodeCurrNeighbComponent.node_neighbours = add_neighbours_curr;

                            //AddNeighbour(nodePrevIdComponent, nodeCurrNeighbComponent);

                            break;
                        }
                    }

                }

                // find best neighbour in next collumn
                byte best_forced_id_next = FindClosestRowNeighbour(entity, next_collumn_entities, rows);
                if (best_forced_id_next != 0)
                {
                    Debug.Log($"_!_!_!_!_!_ FOUND BEST FORCED ID TO NEXT _{best_forced_id_next}_");

                    foreach (var ent in next_collumn_entities)
                    {
                        ref var nodePotIdComponent = ref nodeIdStash.Get(ent);
                        if (nodePotIdComponent.node_id == best_forced_id_next)
                        {
                            ref var nodeNextNeighbComponent = ref nodeNeighbStash.Get(ent);
                            ref var nodeNextIdComponent = ref nodeIdStash.Get(ent);

                            Debug.Log($"_!_!_!_!_!_ FOUND CORRECT ID FOR FORCED NEXT CONNECTION FROM _{nodeCurrIdComponent.node_id}_ TO _{nodeNextIdComponent.node_id}_");

                            // add current node id to neighbours of next node
                            List<byte> add_neighbours_next = nodeNextNeighbComponent.node_neighbours;
                            add_neighbours_next.Add(nodeCurrIdComponent.node_id);
                            nodeNextNeighbComponent.node_neighbours = add_neighbours_next;
                            
                            //AddNeighbour(nodeCurrIdComponent, nodeNextNeighbComponent);

                            // add next node id to neighbours of current node
                            List<byte> add_neighbours_curr = nodeCurrNeighbComponent.node_neighbours;
                            add_neighbours_curr.Add(nodeNextIdComponent.node_id);
                            nodeCurrNeighbComponent.node_neighbours = add_neighbours_curr;

                            //AddNeighbour(nodeNextIdComponent, nodeCurrNeighbComponent);
                        }
                    }
                }

                
                
            }



        }

        nodeWorld.Commit();



        // ----------------------------------- Third walkthrough - give specific types of events to all nodes




        MapUpdate();
        return;
    }

    private void AddNeighbour(MapNodeIdComponent id_to_add, MapNodeNeighboursComponent NeighbComponent)
    {
        List<byte> add_neighbours = NeighbComponent.node_neighbours;
        add_neighbours.Add(id_to_add.node_id);
        NeighbComponent.node_neighbours = add_neighbours;
    }


    private byte FindClosestRowNeighbour(Entity base_entity, List<Entity> compare_collumn, int max_rows)
    {

        // EASIER AND SMARTER SOLLUTION, BUT MAY BACKFIRE IF GENERATION GETS FUCKED AT SOME POINT
        // ok, so the logic ahead is as follows:
        // if a node does not has a connection on either or both sides of its collumn, then its a dead end
        // all neighbours before this node (on the left) have a smaller id
        // all neighbours after this node (on the right) have a bigger id
        // so if a node has only neighbours that are either bigger or smaller then itself, it means it is a dead end

        // SAFER OPTION SINCE ITS A DIRECT COMPARRISON OF COLLUMN CONNECTION, BUT HARDER TO WRITE AND LOOKS STUPID
        // there exists an alternative way of calculating:
        // check each individual neighbours collumn to see if there are direct lack of collumn connection

        // FOR NOW THE SAFER OPTION WAS IMPLEMENTED

        //Entity best_forced_entity = new();
        byte best_forced_id = 0;

        ref var nodeCurrNeighbComponent = ref nodeNeighbStash.Get(base_entity);
        ref var nodeCurrIdComponent = ref nodeIdStash.Get(base_entity);
        ref var nodeCurrPosComponent = ref nodePosStash.Get(base_entity);

        Debug.Log("#######################################                  SEARCHING FOR CLOSEST ROW NEIGHBOUR");
        Debug.Log($"#######################################                  base entity id : _{nodeCurrIdComponent.node_id}_");


        bool flag_got_collumn_neighb = false;
        foreach (var potential_entity in compare_collumn)
        {
            ref var nodePotIdComponent = ref nodeIdStash.Get(potential_entity);

            if (nodeCurrNeighbComponent.node_neighbours.Contains(nodePotIdComponent.node_id))
            {
                Debug.Log($"#######################################                  found connection id : _{nodePotIdComponent.node_id}_");

                flag_got_collumn_neighb = true;
                break;
            }
            
        }

        if (!flag_got_collumn_neighb)
        {
            Debug.Log("#######################################                  not found connection on this side, proceding to search for forced connection");

            byte temp_entity_choice = 0;// = (byte)nodeCurrPosComponent.node_row;
            int temp_row_diff = max_rows * 2;

            // for increased randomness we can shuffle the prev collumn array

            foreach (var potential_entity in compare_collumn)
            {
                ref var nodePotIdComponent = ref nodeIdStash.Get(potential_entity);
                ref var nodePotPosComponent = ref nodePosStash.Get(potential_entity);

                var temp_curr_diff = Math.Abs(nodePotPosComponent.node_row - nodeCurrPosComponent.node_row);

                if (temp_curr_diff < temp_row_diff)
                {
                    temp_entity_choice = nodePotIdComponent.node_id;

                    temp_row_diff = temp_curr_diff;

                    if (temp_row_diff == 0) { break; }
                }
            }

            best_forced_id = temp_entity_choice;
        }
        else
        {
            Debug.Log($"#######################################                  found connection CONFIRM END OF CYCLE");

        }

        return best_forced_id;
    }

    private List<Entity> SearchForEntitiesOfCollumn(byte collumn)
    {
        Debug.Log($"---------- searching for entities in collumn _{collumn}_ ----------");

        //this.filterPos = this.nodeWorld.Filter.With<MapNodePositionComponent>().Build();

        var debug_log = new List<byte>();

        List<Entity> result = new List<Entity>();

        foreach (var entity in this.filterPos)
        {
            ref var nodePosComponent = ref nodePosStash.Get(entity);
            ref var nodeIdComponent = ref nodeIdStash.Get(entity);

            if (nodePosComponent.node_collumn == collumn)
            {
                result.Add(entity);
                debug_log.Add(nodeIdComponent.node_id);
            }
        }

        string combinedString = string.Join(",", debug_log.ToArray());

        Debug.Log($"---------- result : _{combinedString}_");


        return result;
    }




    // this function will return a random row position, using past collumn rows and an offset
    // that can be equal to -1, 0 or +1
    // the final value is clamped to be in set bounds 
    private byte RollForCurrentRow(byte[] temp_past_coll, byte rows)
    {
        var roll = Random.Range(0, temp_past_coll.Length);

        var val = (temp_past_coll[roll]);
        var offs = Random.Range(-1, 2);

        var temp_curr_row = val + offs;

        temp_curr_row = Math.Clamp(temp_curr_row, 0, rows);

        Debug.Log($" ######  Rolled for Current Row, got _index {roll}_ , _equals to {val}_ , _with offset {offs}_ , itogo - _{val + offs}_ , clamped - _{temp_curr_row}_");

        byte result = Convert.ToByte(temp_curr_row);

        return result;
    }

    // this function will march along a collumn upside or downside, checking to see if a new free position is discovered
    // which side is chosen to march along gets determined randomly
    private byte MarchOnCollumn(List<byte> list, byte init_row, byte rows)
    {

        int temp_marcher = init_row;

        int[] temp_steps = new int[2];
        temp_steps[0] = 1;
        temp_steps[1] = -1;

        int temp_direction = temp_steps[Random.Range(0, 1)];


        Debug.Log($" ___________    STARTED MARCHING FOR ROW {init_row} with direction {temp_direction}  ___________");


        bool temp_flag_march = true;

        while (temp_flag_march)
        {
            temp_marcher += temp_direction;

            if (temp_marcher > rows)
            {
                Debug.LogWarning(" ___________    MARCHING LOOPED TO MIN   ___________"); 
                temp_marcher = 0; }
            if (temp_marcher < 0)
            {
                Debug.LogWarning(" ___________    MARCHING LOOPED TO MAX   ___________"); 
                temp_marcher = rows; }

            temp_flag_march = CheckForListCollision(list, (byte)temp_marcher);

            // failsafe if we went all around the collumn
            if (temp_marcher == init_row)
            {
                Debug.LogError(" ___________    MARCHING MADE A FULL LOOP   ___________");
                break; 
            }
        }

        Debug.Log($" ___________    ENDED MARCHING WITH {(byte)temp_marcher}   ___________");

        return (byte)temp_marcher;
    }

    // this function will check for an existance of value inside of a list
    private bool CheckForListCollision(List<byte> list, byte value)
    {
        bool flag = false;

        // check to see if there already are occupied position of same value
        foreach (byte pos in list)
        {
            // if it is, then we need to march to find the next best position
            if (value == pos)
            {
                flag = true;
                break;
            }
        }

        return flag;
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
