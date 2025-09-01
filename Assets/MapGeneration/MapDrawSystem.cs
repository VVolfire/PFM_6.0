using Scellecs.Morpeh;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public sealed class MapDrawSystem : MonoBehaviour , ISystem 
{
    public World World { get; set; }

    private Filter filterPos;
    private Filter filterId;

    private Filter filterBG;

    private Stash<MapNodePositionComponent> nodePosStash;
    private Stash<MapNodeIdComponent> nodeIdStash;
    private Stash<MapNodeNeighboursComponent> nodeNeighbStash;

    private Stash<MapBGComponent> bgStash;

    private Transform Lines;
    private Transform Nodes;

    public GameObject bgPrefab;
    public GameObject nodePrefab;
    public Material lineMaterial;
    private LineRenderer lineRenderer;


    public void OnAwake()
    {

        Debug.Log("NodeDrawSys is Awake");

        this.filterPos = this.World.Filter.With<MapNodePositionComponent>().Build();
        this.filterId = this.World.Filter.With<MapNodeIdComponent>().Build();


        this.nodePosStash = this.World.GetStash<MapNodePositionComponent>();
        this.nodeIdStash = this.World.GetStash<MapNodeIdComponent>();
        this.nodeNeighbStash = this.World.GetStash<MapNodeNeighboursComponent>();


        this.filterBG = this.World.Filter.With<MapBGComponent>().Build();
        this.bgStash = this.World.GetStash<MapBGComponent>();

        Lines = new GameObject("LinesContainer").transform;
        Nodes = new GameObject("NodesContainer").transform;
    }

    public void OnUpdate(float deltaTime)
    {
        Debug.Log("NodeDrawSys is Updating");
        foreach (var entity in this.filterPos)
        {
            ref var nodePosComponent = ref nodePosStash.Get(entity);
            ref var nodeIdComponent = ref nodeIdStash.Get(entity);
            ref var nodeNeighbComponent = ref nodeNeighbStash.Get(entity);

            //Debug.Log(nodePosComponent.node_x);
            //Debug.Log(nodePosComponent.node_y); 

            var prefabedNode = Instantiate(nodePrefab, 
                new Vector3(
                    nodePosComponent.node_x + nodePosComponent.node_x_offset, 
                    nodePosComponent.node_y + nodePosComponent.node_y_offset, 
                    0), 
                Quaternion.identity);
            
            prefabedNode.GetComponent<TextMeshPro>().text = nodeIdComponent.node_id.ToString();

            var max_count = nodeNeighbComponent.node_neighbours.Count;
            for (int i = 0; i < max_count; i++)
            {
                foreach (var neighbour in this.filterId)
                {
                    ref var nodeNeighbPosComponent = ref nodePosStash.Get(neighbour);
                    ref var nodeNeighbIdComponent = ref nodeIdStash.Get(neighbour);

                    if (nodeNeighbComponent.node_neighbours.Contains(nodeNeighbIdComponent.node_id))
                    {
                        //For creating line renderer object
                        lineRenderer = new GameObject("Line").AddComponent<LineRenderer>();
                        lineRenderer.startColor = Color.black;
                        lineRenderer.endColor = Color.black;
                        lineRenderer.startWidth = 3.0f;
                        lineRenderer.endWidth = 3.0f;
                        lineRenderer.positionCount = 2;
                        lineRenderer.useWorldSpace = true;
                        lineRenderer.material = lineMaterial;
                        //lineMaterial.SetColor("_Color", Color.yellow);

                        //For drawing line in the world space, provide the x,y,z values
                        lineRenderer.SetPosition(0, new Vector3(
                            nodePosComponent.node_x + nodePosComponent.node_x_offset, 
                            nodePosComponent.node_y + nodePosComponent.node_y_offset, 0)
                            ); //x,y and z position of the starting point of the line
                        lineRenderer.SetPosition(1, new Vector3(
                            nodeNeighbPosComponent.node_x + nodeNeighbPosComponent.node_x_offset, 
                            nodeNeighbPosComponent.node_y + nodeNeighbPosComponent.node_y_offset, 0)
                            ); //x,y and z position of the end point of the line
                    
                        lineRenderer.transform.SetParent(Lines, true);
                    }
                }

            }

            prefabedNode.transform.SetParent(Nodes, true);
        }

        foreach (var bg in filterBG)
        {
            ref var bgComponent = ref bgStash.Get(bg);
            var scaleChange = new Vector3((bgComponent.scale_x), 1f, 1f);
            var bg_instance = Instantiate(bgPrefab, new Vector3(bgComponent.pos_x, bgComponent.pos_y, 0), Quaternion.identity);
            bg_instance.transform.localScale = scaleChange;
            bg_instance.GetComponent<SpriteRenderer>().sprite = bgComponent.sprite;
            bg_instance.GetComponent<SpriteRenderer>().sortingOrder = bgComponent.layer;
        }

    }

    public void Dispose()
    {
        //Debug.Log("NodeDrawSys is Disposing");
        //throw new System.NotImplementedException();
    }

}
