using Scellecs.Morpeh;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;

public sealed class MapNodeDrawSystem : MonoBehaviour , ISystem 
{
    public World World { get; set; }

    private Filter filterPos;
    private Filter filterId;
    private Stash<MapNodePositionComponent> nodePosStash;
    private Stash<MapNodeIdComponent> nodeIdStash;
    private Stash<MapNodeNeighboursComponent> nodeNeighbStash;

    private Transform Lines;

    public GameObject myPrefab;
    private LineRenderer lineRenderer;

    public void OnAwake()
    {

        Debug.Log("NodeDrawSys is Awake");

        this.filterPos = this.World.Filter.With<MapNodePositionComponent>().Build();
        this.filterId = this.World.Filter.With<MapNodeIdComponent>().Build();


        this.nodePosStash = this.World.GetStash<MapNodePositionComponent>();
        this.nodeIdStash = this.World.GetStash<MapNodeIdComponent>();
        this.nodeNeighbStash = this.World.GetStash<MapNodeNeighboursComponent>();

        Lines = new GameObject("LinesContainer").transform;
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

            var prefabedNode = Instantiate(myPrefab, new Vector3(nodePosComponent.node_x, nodePosComponent.node_y, 0), Quaternion.identity);
            
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
                        lineRenderer.startWidth = 4.0f;
                        lineRenderer.endWidth = 4.0f;
                        lineRenderer.positionCount = 2;
                        lineRenderer.useWorldSpace = true;

                        //For drawing line in the world space, provide the x,y,z values
                        lineRenderer.SetPosition(0, new Vector3(nodePosComponent.node_x, nodePosComponent.node_y, 0)); //x,y and z position of the starting point of the line
                        lineRenderer.SetPosition(1, new Vector3(nodeNeighbPosComponent.node_x, nodeNeighbPosComponent.node_y, 0)); //x,y and z position of the end point of the line
                    
                        lineRenderer.transform.SetParent(Lines, true);
                    }
                }

            }

        }
    }

    public void Dispose()
    {
        //Debug.Log("NodeDrawSys is Disposing");
        //throw new System.NotImplementedException();
    }

}
