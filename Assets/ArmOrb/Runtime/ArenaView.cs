using UnityEngine;

namespace ArmOrb
{
    public sealed class ArenaView : MonoBehaviour
    {
        public LineRenderer boundary;
        public Camera arenaCamera;
        Mesh dangerMesh;
        MeshRenderer dangerRenderer;
        LineRenderer circle;
        readonly Vector3[] vertices=new Vector3[256],linePoints=new Vector3[128];
        readonly int[] triangles=new int[128*6];
        readonly Color[] colors=new Color[256];
        float halfSize;
        public void Configure(float size)
        {
            halfSize=size*0.5f;boundary.positionCount=4;boundary.loop=true;
            boundary.SetPositions(new[]{new Vector3(-halfSize,-halfSize),new Vector3(-halfSize,halfSize),new Vector3(halfSize,halfSize),new Vector3(halfSize,-halfSize)});
            boundary.widthMultiplier=size*0.004f;arenaCamera.orthographicSize=halfSize+size*0.025f;ClearDanger();
        }
        void CreateDanger()
        {
            var go=new GameObject("Danger zone");go.transform.SetParent(transform,false);
            var filter=go.AddComponent<MeshFilter>();dangerMesh=new Mesh{name="Danger outside circle"};dangerMesh.MarkDynamic();filter.sharedMesh=dangerMesh;
            dangerRenderer=go.AddComponent<MeshRenderer>();dangerRenderer.sharedMaterial=boundary.sharedMaterial;dangerRenderer.sortingOrder=-1;
            var ring=new GameObject("Safe circle");ring.transform.SetParent(transform,false);circle=ring.AddComponent<LineRenderer>();circle.sharedMaterial=boundary.sharedMaterial;
            circle.loop=true;circle.useWorldSpace=false;circle.positionCount=128;circle.widthMultiplier=0.045f;circle.startColor=circle.endColor=new Color(0.95f,0.36f,0.12f);circle.sortingOrder=1;
            for(int i=0;i<128;i++)
            {
                int next=(i+1)%128;int t=i*6;triangles[t]=i*2;triangles[t+1]=next*2;triangles[t+2]=i*2+1;
                triangles[t+3]=next*2;triangles[t+4]=next*2+1;triangles[t+5]=i*2+1;
            }
        }
        public void DrawDanger(ArenaSimulation simulation)
        {
            if(!simulation.Danger||simulation.Elapsed<90){ClearDanger();return;}
            if(dangerMesh==null)CreateDanger();dangerRenderer.enabled=true;circle.enabled=simulation.SafeRadius>0;

            for(int i=0;i<128;i++)
            {
                float a=i*Mathf.PI*2/128;var direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                float outer=halfSize/Mathf.Max(Mathf.Abs(direction.x),Mathf.Abs(direction.y));
                vertices[i*2]=direction*outer;vertices[i*2+1]=direction*Mathf.Min(outer,simulation.SafeRadius);linePoints[i]=vertices[i*2+1];
                colors[i*2]=colors[i*2+1]=new Color(1,0.35f,0.13f,0.18f);
            }
            dangerMesh.vertices=vertices;dangerMesh.colors=colors;dangerMesh.triangles=triangles;dangerMesh.RecalculateBounds();circle.SetPositions(linePoints);
        }
        public void ClearDanger(){if(dangerRenderer)dangerRenderer.enabled=false;if(circle)circle.enabled=false;}
        void OnDestroy(){if(dangerMesh)Destroy(dangerMesh);}
    }
}
