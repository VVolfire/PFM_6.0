using Scellecs.Morpeh;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
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

    public Stash<MapBGComponent> bgStash;

    public int whole_map_middle_y_point;

    public int bg_segment_lenght;
    public int bg_startend_lenght;
    public int bg_beginning_of_scroll;

    public Sprite bg_big_sprite;
    public Sprite bg_start_sprite;
    public Sprite bg_end_sprite;
    public List<Sprite> bg_segment_sprites;

    //public GameObject UIBGPrefab;
    //public GameObject UIMapSegmentPrefab;
    //public GameObject UIMapStartPrefab;
    //public GameObject UIMapEndPrefab;

    public GameObject bgPrefab;
    public GameObject nodePrefab;

    // collumn_count does not include start and end nodes, only path in between
    public byte collumn_count = 9;
    // row_count represents maximum POSSIBLE amount of rows (including zero), but will try to be belowe that point
    public byte row_count = 3;

    // icon_bounding_box_width this is a bounding box width that is used to check if the icons are colliding
    public byte icon_bounding_box_width;

    public Material lineMaterial;

    // map_hor_offset is the coordinate offset from screen borders on both horizontal sides of screen, from left to start and from right to end
    public int map_hor_offset = 130;

    // map_hor_start is a representation of left horizontal border point from which the map "canvas" starts, i.e. a map border
    public int map_hor_start = 0;
    // map_hor_dist a distance between collumns
    public int map_hor_dist = 110; // ----->>>  REMEMBER ABOUT RANDOM HOR OFFSET
    // map_hor_random_max corresponds to maximum possible horizontal random offset of node in addition to base offset, this mean -N to +N
    // this end value will be increased by map_hor_random_increase, to get more abrupt change
    public int map_hor_random_max = 3;
    public int map_hor_random_increase = 4;

    // map_vert_offset is the coordinate offset from screen borders on both vertical sides of screen
    public int map_vert_offset = 70;

    // map_vert_start is a representation of upper vertical border point from which the map "canvas" starts, i.e. a map border
    public int map_vert_start = 0;
    // map_vert_end same as above but for lower border
    public int map_vert_end = 1080 / 3; // we use target resolution that is 1080. Our style resolution is 360 |=> Divide by 3 ..... this is here as a reminder
    // map_vert_random_max corresponds to maximum possible vertical random offset of node in addition to base offset, this mean -N to +N
    // this end value will be increased by map_vert_random_increase, to get more abrupt change
    public int map_vert_random_max = 4;
    public int map_vert_random_increase = 4;
    // map_vert_pull_strenght is a modifier that increments the end result of difference between correct position of node and its clear position
    public float map_vert_pull_strenght = 0.7f;

    public void Start()
    {
        Debug.Log("MapController is Starting");

        //nodeWorld = World.Default;
        nodeWorld = World.Create();

        nodeWorld.UpdateByUnity = false;



        //var newEntity = newWorld.CreateEntity();
        //newWorld.RemoveEntity(newEntity);

        var newSystem = new MapDrawSystem();

        newSystem.nodePrefab = nodePrefab;
        newSystem.bgPrefab = bgPrefab;
        newSystem.lineMaterial = lineMaterial;

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
        //Debug.Log("MapController is GeneratingMap");

        nodeIdStash = nodeWorld.GetStash<MapNodeIdComponent>();
        nodePosStash = nodeWorld.GetStash<MapNodePositionComponent>();
        nodeNeighbStash = nodeWorld.GetStash<MapNodeNeighboursComponent>();

        List<Entity> prev_collumn_entities = new List<Entity>();
        List<Entity> current_collumn_entities = new List<Entity>();
        List<Entity> next_collumn_entities = new List<Entity>();


        int total_lenght = 0;



        // ----------------------------------- First walkthrough - generate nodes and give IDs 
        Debug.LogWarning("----------------------------------- First walkthrough - generate nodes and give IDs");

        #region
        // generation of first node, without any offset
        var entityFirst = nodeWorld.CreateEntity();

        var first_x = map_hor_start + map_hor_offset;

        nodeIdStash.Set(entityFirst, new MapNodeIdComponent { node_id = 0 });
        nodePosStash.Set(entityFirst, new MapNodePositionComponent
        {
            node_x = first_x,
            node_y = map_vert_end/2,
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

        int temp_end_x = 0;
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

                //var diff = (map_hor_end-map_hor_offset)-(map_hor_start + map_hor_offset);

                var temp_x = (int)(map_hor_start + map_hor_offset + map_hor_dist * i);
                temp_end_x = temp_x + map_hor_dist;
                //var temp_x = (int)(map_hor_start + map_hor_offset + (diff / collumns) * i);
                var temp_y = (int)(map_vert_start + map_vert_offset + ((map_vert_end - map_vert_offset*2) / rows) * temp_curr_row);

                

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
            node_x = temp_end_x,
            node_y = map_vert_end / 2,
            node_collumn = collumns + 1,
            node_row = rows / 2
        });

        total_lenght = temp_end_x - first_x + map_hor_offset;

        nodeWorld.Commit();
        #endregion

        // ----------------------------------- Second walkthrough - create connections between nodes
        Debug.LogWarning("----------------------------------- Second walkthrough - create connections between nodes");

        #region

        nodeNeighbStash.Set(entityFirst, new MapNodeNeighboursComponent { node_neighbours = new List<byte>() });


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




        Debug.Log(".......................................GETTING RID OF DEAD ENDS.......................................");
        // SECOND WALKTHROUGH TO GET RID OF DEAD ENDS


        for (byte i = 1; i <= collumns; i++)
        {
            //Debug.Log($"_______________ forcing connection on collumn {i} _______________");
            //Debug.Log("_____ prev collumn _____");
            prev_collumn_entities = SearchForEntitiesOfCollumn((byte)(i - 1));

            //Debug.Log("_____ curr collumn _____");
            // need to fill current_collumn_entities list with entities of current collumn
            current_collumn_entities = SearchForEntitiesOfCollumn(i);

            //Debug.Log("_____ next collumn _____");
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
        #endregion

        // ----------------------------------- Third walkthrough - give propper offset
        Debug.LogWarning("----------------------------------- Third walkthrough -  give propper offset");

        #region

        //Debug.Log(".......................................GIVING OFFSET.......................................");
        // THIRD WALKTHROUGH TO GIVE OFFSET


        // the base logic for giving offset is to find all connected neighbours rows and find the average row value between them
        // this logic MUST include the start row of the node itself
        for (byte i = 1; i <= collumns; i++)
        {
            //Debug.Log($"_______________ giving offset on collumn {i} _______________");
            //Debug.Log("_____ prev collumn _____");
            prev_collumn_entities = SearchForEntitiesOfCollumn((byte)(i - 1));
            
            //Debug.Log("_____ curr collumn _____");
            // need to fill current_collumn_entities list with entities of current collumn
            current_collumn_entities = SearchForEntitiesOfCollumn(i);

            //Debug.Log("_____ next collumn _____");
            // need to fill current_collumn_entities list with entities of current collumn
            next_collumn_entities = SearchForEntitiesOfCollumn((byte)(i + 1));



            foreach (var entity in current_collumn_entities)
            {
                // get current entity info
                ref var nodeCurrNeighbComponent = ref nodeNeighbStash.Get(entity);
                ref var nodeCurrPosComponent = ref nodePosStash.Get(entity);
                ref var nodeCurrIdComponent = ref nodeIdStash.Get(entity);

                Debug.Log($"------------------------- getting offset for {nodeCurrIdComponent.node_id} -------------------------");

                float temp_row_summ = nodeCurrPosComponent.node_row;
                float temp_neighb_count = nodeCurrNeighbComponent.node_neighbours.Count + 1; // +1 since the initial row MUST be counted

                Debug.Log($"------------------------- CURRENT    row summ {temp_row_summ} and neighb count {temp_neighb_count}-------------------------");

                var temp_prev_summ = GetRowSummOfNeighbours(prev_collumn_entities, nodeCurrNeighbComponent, nodeCurrPosComponent.node_row);
                temp_row_summ += temp_prev_summ;

                Debug.Log($"------------------------- AFTER PREV row summ {temp_row_summ} and neighb count {temp_neighb_count}-------------------------");

                var temp_next_summ = GetRowSummOfNeighbours(next_collumn_entities, nodeCurrNeighbComponent, nodeCurrPosComponent.node_row);
                temp_row_summ += temp_next_summ;

                Debug.Log($"------------------------- AFTER NEXT row summ {temp_row_summ} and neighb count {temp_neighb_count}-------------------------");

                // find the average row
                var temp_average_row = temp_row_summ / temp_neighb_count;

                Debug.Log($"------------------------- average row {temp_average_row} -------------------------");

                //var temp_y = (int)(map_vert_start + map_vert_offset + ((map_vert_end - map_vert_offset * 2) / rows) * temp_curr_row);
                var temp_y_correct = map_vert_start + map_vert_offset + ((map_vert_end - map_vert_offset * 2) / rows) * (temp_average_row);
                var temp_offset_rand = Random.Range(-map_vert_random_max, map_vert_random_max) * map_vert_random_increase;

                var temp_y_end_pos = temp_y_correct + temp_offset_rand; 
                temp_y_end_pos = Math.Clamp(temp_y_end_pos, map_vert_start + map_vert_offset, map_vert_end - map_vert_offset);

                var temp_y_offset = (int)((temp_y_end_pos - nodeCurrPosComponent.node_y) * map_vert_pull_strenght);

                Debug.Log($"------------------------- current y {nodeCurrPosComponent.node_y} -------------------------");
                Debug.Log($"------------------------- correct y {temp_y_correct} -------------------------");
                Debug.Log($"------------------------- randome y {temp_offset_rand} -------------------------");
                Debug.Log($"------------------------- endpose y {temp_y_end_pos} -------------------------");
                Debug.Log($"------------------------- !FINAL! y {temp_y_offset/ map_vert_pull_strenght} * {map_vert_pull_strenght} = {temp_y_offset} -------------------------");

                nodeCurrPosComponent.node_y_offset = temp_y_offset;



                var temp_x_offset = Random.Range(-map_hor_random_max, map_hor_random_max) * map_hor_random_increase;
                

                nodeCurrPosComponent.node_x_offset = temp_x_offset;
            }
        }




        nodeWorld.Commit();
        #endregion

        // ----------------------------------- Fourth walkthrough - get rid of icon intersections
        Debug.LogWarning("----------------------------------- Fourth walkthrough - get rid of icon intersections");

        #region


        for (byte i = 1; i <= collumns + 1; i++)
        {
            // need to fill current_collumn_entities list with entities of current collumn
            current_collumn_entities = SearchForEntitiesOfCollumn(i);


            foreach (var entity in current_collumn_entities)
            {

                var temp_else_entities = SearchForEntitiesOfCollumn(i);
                temp_else_entities.Remove(entity);


                ref var nodeIdComponent = ref nodeIdStash.Get(entity);
                ref var nodePosComponent = ref nodePosStash.Get(entity);
                var total_curr_y = nodePosComponent.node_y_offset + nodePosComponent.node_y;
                var debug_flag = false;


                if (temp_else_entities.Count != 0)
                {
                    foreach (var else_entity in temp_else_entities)
                    {
                        ref var nodeElseIdComponent = ref nodeIdStash.Get(else_entity);
                        ref var nodeElsePosComponent = ref nodePosStash.Get(else_entity);
                        var total_else_y = nodeElsePosComponent.node_y_offset + nodeElsePosComponent.node_y;


                        if ((total_else_y >= total_curr_y - (icon_bounding_box_width + icon_bounding_box_width / 2))
                        && (total_else_y <= total_curr_y + (icon_bounding_box_width + icon_bounding_box_width / 2)))
                        {
                            Debug.Log($"__________ ICON INTERSECTION AT CURR ID   {nodeIdComponent.node_id}  and ELSE ID  {nodeElseIdComponent.node_id}");
                            debug_flag = true;
                            Debug.Log($" PRE change curr Y POS:  {total_curr_y}  ");
                            Debug.Log($" PRE change else Y POS:  {total_else_y}  ");

                            // curr y pos is belowe the middle
                            if (total_curr_y <= (map_vert_end + map_vert_start) / 2)
                            {
                                // curr y pos is belowe the other y pos
                                if (total_curr_y < total_else_y)
                                {
                                    total_curr_y -= (int)(icon_bounding_box_width * 0.5);
                                    total_else_y += (int)(icon_bounding_box_width * 0.5);
                                }
                                // curr y pos is above the other y pos
                                else
                                {
                                    total_curr_y += (int)(icon_bounding_box_width * 0.5);
                                    total_else_y -= (int)(icon_bounding_box_width * 0.5);
                                }
                            }
                            // curr y pos is above the middle
                            else
                            {
                                // curr y pos is belowe the other y pos
                                if (total_curr_y < total_else_y)
                                {
                                    total_curr_y -= (int)(icon_bounding_box_width * 0.5);
                                    total_else_y += (int)(icon_bounding_box_width * 0.5);
                                }
                                // curr y pos is above the other y pos
                                else
                                {
                                    total_curr_y += (int)(icon_bounding_box_width * 0.5);
                                    total_else_y -= (int)(icon_bounding_box_width * 0.5);
                                }
                            }

                        }


                        total_else_y = Math.Clamp(total_else_y, map_vert_start + map_vert_offset, map_vert_end - map_vert_offset);

                        if (debug_flag)
                        {
                            Debug.Log($" TOTAL ELSE Y POS:  {total_else_y}  ");
                            Debug.Log($" FINAL ELSE Y POS:  {total_else_y - nodeElsePosComponent.node_y_offset}  ");
                        }

                        nodeElsePosComponent.node_y = total_else_y - nodeElsePosComponent.node_y_offset;
                    }
                }


                total_curr_y = Math.Clamp(total_curr_y, map_vert_start + map_vert_offset, map_vert_end - map_vert_offset);

                if (debug_flag)
                {
                    Debug.Log($" TOTAL CURR Y POS:  {total_curr_y}  ");
                    Debug.Log($" FINAL CURR Y POS:  {total_curr_y - nodePosComponent.node_y_offset}  ");
                }

                nodePosComponent.node_y = total_curr_y - nodePosComponent.node_y_offset;

            }

        }


        nodeWorld.Commit();
        #endregion


        // ----------------------------------- Fifthfth walkthrough - create the Back Ground 
        Debug.LogWarning("----------------------------------- Fifthfth walkthrough - create the Back Ground");

        #region


        // layer -10 : Two Base BGs to fill the space before the map itself, the second one is inverted by X
        bgStash = nodeWorld.GetStash<MapBGComponent>();

        var bg_entity = nodeWorld.CreateEntity();
        bgStash.Set(bg_entity, new MapBGComponent 
        { 
            sprite = bg_big_sprite, 
            pos_x = (int)(bg_big_sprite.rect.width/2), 
            pos_y = whole_map_middle_y_point, 
            scale_x = 1,
            layer = -10
        });

        bg_entity = nodeWorld.CreateEntity();
        bgStash.Set(bg_entity, new MapBGComponent
        {
            sprite = bg_big_sprite,
            pos_x = (int)(bg_big_sprite.rect.width + bg_big_sprite.rect.width / 2),
            pos_y = whole_map_middle_y_point,
            scale_x = -1,
            layer = -10
        });

        //var scaleChange = new Vector3(-1f, 1f, 1f);
        //Instantiate(UIBGPrefab, new Vector3(500, 180, 0), Quaternion.identity);
        //Instantiate(UIBGPrefab, new Vector3(1500, 180, 0), Quaternion.identity).transform.localScale = scaleChange;



        // layer -9 : Start of Map Scroll
        var scroll_start = bg_beginning_of_scroll + bg_startend_lenght / 2;
        bg_entity = nodeWorld.CreateEntity();
        bgStash.Set(bg_entity, new MapBGComponent
        {
            sprite = bg_start_sprite,
            pos_x = scroll_start,
            pos_y = whole_map_middle_y_point,
            scale_x = 1,
            layer = -9
        });
        //Instantiate(UIMapStartPrefab, new Vector3(scroll_start, 180, 0), Quaternion.identity);


        // layer -8 : Segments of Map Scroll
        // first segment
        var segment_start = scroll_start + bg_startend_lenght / 2 + bg_segment_lenght / 2;
        var segment_spr_count = bg_segment_sprites.Count;
        var rand_spr_id = (byte)Random.Range(0, segment_spr_count);
        var excluded_spr_id = rand_spr_id;

        bg_entity = nodeWorld.CreateEntity();
        bgStash.Set(bg_entity, new MapBGComponent
        {
            sprite = bg_segment_sprites[rand_spr_id],
            pos_x = segment_start,
            pos_y = whole_map_middle_y_point,
            scale_x = 1,
            layer = -8
        });

        //var segment_first = Instantiate(UIMapSegmentPrefab, new Vector3(segment_start, 180, 0), Quaternion.identity);
        //var segment_spr_count = segment_first.GetComponent<Scr_MapVisualSegment>().sprites.Count;

        //segment_first.GetComponent<Scr_MapVisualSegment>().SpriteUpdate(rand_spr_id);

        // count the total numb of segments, using total lenght minus the already added first segment
        var segment_count = Math.Ceiling((double)(total_lenght / bg_segment_lenght) - 1);
        var latest_x = segment_start + bg_segment_lenght;
        for (byte i = 0; i < segment_count; i++)
        {
            // instantiate a segment
            //var segment = Instantiate(UIMapSegmentPrefab, new Vector3(latest_x, 180, 0), Quaternion.identity);

            // roll for valid sprite id, that means not an id that was in the previous segment
            while (true)
            {
                rand_spr_id = (byte)Random.Range(0, segment_spr_count);
                if (rand_spr_id != excluded_spr_id)
                {
                    excluded_spr_id = rand_spr_id;
                    break;
                }
            }

            bg_entity = nodeWorld.CreateEntity();
            bgStash.Set(bg_entity, new MapBGComponent
            {
                sprite = bg_segment_sprites[rand_spr_id],
                pos_x = latest_x,
                pos_y = whole_map_middle_y_point,
                scale_x = 1,
                layer = -8
            });

            // add total coordinates count
            latest_x += bg_segment_lenght;
        }

        // layer -9 : End of Map Scroll
        //Instantiate(UIMapEndPrefab, new Vector3(latest_x - bg_segment_lenght/2 + bg_startend_lenght / 2, 180, 0), Quaternion.identity);

        var scroll_end = latest_x - bg_segment_lenght / 2 + bg_startend_lenght / 2;
        bg_entity = nodeWorld.CreateEntity();
        bgStash.Set(bg_entity, new MapBGComponent
        {
            sprite = bg_end_sprite,
            pos_x = scroll_end,
            pos_y = whole_map_middle_y_point,
            scale_x = 1,
            layer = -9
        });



        nodeWorld.Commit();
        #endregion


        // ----------------------------------- Sixth micro walkthrough - centrilize all of the nodes closer to center of map scroll 
        Debug.LogWarning("----------------------------------- Sixth micro walkthrough - centrilize all of the nodes closer to center of map scroll");

        #region

        var total_bg_distance = scroll_end - scroll_start;
        var scroll_bg_diff = total_bg_distance - total_lenght;
        var final_x_adjustment = scroll_bg_diff/2;

        foreach (var entity in filterPos)
        {

            ref var nodePosComponent = ref nodePosStash.Get(entity);
            nodePosComponent.node_x_offset += final_x_adjustment;

        }


        nodeWorld.Commit();
        #endregion


        // ----------------------------------- Seventh walkthrough - give specific types of events to all nodes
        Debug.LogWarning("----------------------------------- Seventh walkthrough - give specific types of events to all nodes");

        #region



        nodeWorld.Commit();
        #endregion


        MapUpdate();
        return;
    }

    private void AddNeighbour(MapNodeIdComponent id_to_add, MapNodeNeighboursComponent NeighbComponent)
    {
        List<byte> add_neighbours = NeighbComponent.node_neighbours;
        add_neighbours.Add(id_to_add.node_id);
        NeighbComponent.node_neighbours = add_neighbours;
    }

    private float GetRowSummOfNeighbours(List<Entity> collumn_entities, MapNodeNeighboursComponent nodeCurrNeighbComponent, int currRow)
    {

        float temp_row_summ = 0;

        foreach (var entity in collumn_entities)
        {
            ref var nodePrevIdComponent = ref nodeIdStash.Get(entity);
            ref var nodePrevPosComponent = ref nodePosStash.Get(entity);

            if (nodeCurrNeighbComponent.node_neighbours.Contains(nodePrevIdComponent.node_id))
            {

                // if its the adjacent row then add only half the value? Potential fix for the overlaping of nodes
                if (Math.Abs(nodePrevPosComponent.node_row - currRow) == 1)
                {
                    // 3 - (3 - 2) * 0.5 = 3 - 0.5 = 2.5
                    // 1 - (1 - 2) * 0.5 = 1 + 0.5 = 1.5
                    // 2 - (2 - 1) * 0.5 = 2 - 0.5 = 1.5
                    temp_row_summ += nodePrevPosComponent.node_row - (nodePrevPosComponent.node_row - currRow) * 0.5f;

                }
                else
                {

                    temp_row_summ += nodePrevPosComponent.node_row;

                }
            }

        }

        return temp_row_summ;
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
        //Debug.Log($"---------- searching for entities in collumn _{collumn}_ ----------");

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

        //Debug.Log($"---------- result : _{combinedString}_");


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
