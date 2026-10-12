using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Catan.Client
{
    public sealed class BoardTarget : MonoBehaviour { public string Kind; public int Id; }
    public sealed class BoardRenderer : MonoBehaviour
    {
        private Transform root;
        private readonly Dictionary<int,Vector3> positions = new Dictionary<int,Vector3>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Snapshot view;
        private HostResponse response;
        private string focus;
        private int[] previewRoads;
        private int stagedRoad = -1;
        public Camera BoardCamera { get; private set; }
        public static Color PlayerColor(Snapshot s,string owner)
        {
            Color[] colors = {new Color(.93f,.38f,.28f),new Color(.26f,.65f,.96f),new Color(.95f,.78f,.28f),new Color(.83f,.79f,.93f)};
            return owner == null ? new Color(.25f,.34f,.37f) : colors[Math.Max(0,Array.FindIndex(s.Players,p=>p.Id==owner))%4];
        }
        private void Awake()
        {
            var obj=new GameObject("Board camera"); obj.transform.SetParent(transform);
            BoardCamera=obj.AddComponent<Camera>(); BoardCamera.orthographic=true; BoardCamera.orthographicSize=4.7f;
            BoardCamera.transform.position=new Vector3(0,18,-1); BoardCamera.transform.LookAt(Vector3.zero);
            BoardCamera.depth=10; BoardCamera.clearFlags=CameraClearFlags.SolidColor; BoardCamera.backgroundColor=new Color(.07f,.17f,.22f);
        }
        public void SetViewport(Rect pixels)
        {
            BoardCamera.rect=new Rect(pixels.x/Screen.width,1-(pixels.y+pixels.height)/Screen.height,pixels.width/Screen.width,pixels.height/Screen.height);
            // Fit both coast dimensions when the game window is narrow.
            BoardCamera.orthographicSize=Mathf.Max(4.7f,4.6f/Mathf.Max(.1f,pixels.width/pixels.height));
        }
        private Material Paint(Color color)
        {
            var shader=Resources.Load<Shader>("Board"); if(shader==null) throw new InvalidOperationException("Catan board shader could not be loaded.");
            var m=new Material(shader){color=color}; materials.Add(m); return m;
        }
        public void Focus(string piece,int[] secondRoads=null,int first=-1)
        {
            if(focus==piece && previewRoads==secondRoads && stagedRoad==first) return;
            focus=piece; previewRoads=secondRoads; stagedRoad=first; if(response!=null) Render(response);
        }
        public void Render(HostResponse incoming)
        {
            response=incoming; view=incoming.View;
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            foreach(var m in materials)Destroy(m);materials.Clear();foreach(var m in meshes)Destroy(m);meshes.Clear();positions.Clear();
            root=new GameObject("Board").transform;root.SetParent(transform);
            foreach(var h in view.Hexes)
            {
                Vector3 center=new Vector3(Mathf.Sqrt(3)*(h.Q+h.R/2f),0,1.5f*h.R);
                var verts=new Vector3[7]; for(int k=0;k<6;k++){float a=(60*k-30)*Mathf.Deg2Rad;Vector3 c=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));verts[k+1]=c*.96f;positions[h.Vertices[k]]=center+c;}
                var triangles=new int[18];for(int k=0;k<6;k++){triangles[k*3]=0;triangles[k*3+1]=(k+1)%6+1;triangles[k*3+2]=k+1;}
                var mesh=new Mesh{vertices=verts,triangles=triangles};mesh.RecalculateNormals();meshes.Add(mesh);
                var obj=new GameObject("Terrain");obj.transform.SetParent(root);obj.transform.position=center;
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=Paint(TerrainColor(h.Resource));obj.AddComponent<MeshCollider>().sharedMesh=mesh;Target(obj,"Hex",h.Id);
                Label(h.Resource??"Desert",center+new Vector3(0,.04f,.28f),.055f,Color.white);
                if(h.Number>0) Label(h.Number.ToString(),center+new Vector3(0,.06f,-.12f),.13f,h.Number==6||h.Number==8?new Color(1,.85f,.65f):Color.white);
                if(h.Id==view.RobberHex){var robber=Primitive(PrimitiveType.Capsule,center+new Vector3(.47f,.24f,0),new Vector3(.19f,.23f,.19f),new Color(.13f,.12f,.18f));Target(robber,"Hex",h.Id);}
                if(focus=="Robber"&&h.Id!=view.RobberHex) Primitive(PrimitiveType.Sphere,center+Vector3.up*.1f,Vector3.one*.11f,new Color(.97f,.81f,.37f));
            }
            foreach(var e in view.Edges)
            {
                bool legal=ClientActions.Contains(previewRoads??incoming.LegalRoads,e.Id);
                Color color=e.Owner!=null?PlayerColor(view,e.Owner):focus=="Road"&&legal?new Color(.5f,1,.8f):new Color(.21f,.31f,.32f);
                if(e.Id==stagedRoad) color=new Color(.85f,.8f,.4f);
                var obj=Primitive(PrimitiveType.Cube,(positions[e.A]+positions[e.B])/2+Vector3.up*.07f,new Vector3(e.Owner!=null||e.Id==stagedRoad? .15f:.075f,.12f,.82f),color);
                obj.transform.rotation=Quaternion.LookRotation(positions[e.B]-positions[e.A]);Target(obj,"Edge",e.Id);
            }
            foreach(var v in view.Vertices)
            {
                bool legal=ClientActions.Contains(focus=="City"?incoming.LegalCities:incoming.LegalSettlements,v.Id);
                if(v.Level>0) House(positions[v.Id],v.Level,PlayerColor(view,v.Owner),v.Id);
                else {var obj=Primitive(PrimitiveType.Sphere,positions[v.Id]+Vector3.up*.12f,Vector3.one*(legal&&focus=="Settlement"? .18f:.09f),legal&&focus=="Settlement"?new Color(.5f,1,.8f):new Color(.4f,.51f,.5f));Target(obj,"Vertex",v.Id);}
                if(v.Level>0&&focus=="City"&&legal) Primitive(PrimitiveType.Sphere,positions[v.Id]+Vector3.up*.6f,Vector3.one*.15f,new Color(.5f,1,.8f));
            }
            foreach(var p in view.Ports) Label(p.Resource==null?"3:1":p.Resource+" 2:1",(positions[p.A]+positions[p.B])/2*1.15f+Vector3.up*.04f,.04f,new Color(.88f,.86f,.7f));
        }
        private void House(Vector3 p,int level,Color color,int id)
        {
            var house=Primitive(PrimitiveType.Cube,p+Vector3.up*.23f,new Vector3(level==2? .32f:.24f,.26f,.23f),color);Target(house,"Vertex",id);
            var roofVerts=new[]{new Vector3(-.16f,0,-.14f),new Vector3(.16f,0,-.14f),new Vector3(0,.16f,-.14f),new Vector3(-.16f,0,.14f),new Vector3(.16f,0,.14f),new Vector3(0,.16f,.14f)};
            var mesh=new Mesh{vertices=roofVerts,triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3}};mesh.RecalculateNormals();meshes.Add(mesh);
            var roof=new GameObject("Roof");roof.transform.SetParent(root);roof.transform.position=p+Vector3.up*.36f;roof.AddComponent<MeshFilter>().sharedMesh=mesh;roof.AddComponent<MeshRenderer>().sharedMaterial=Paint(color*.8f);
            if(level==2){var tower=Primitive(PrimitiveType.Cube,p+new Vector3(.14f,.35f,0),new Vector3(.16f,.45f,.2f),color);Target(tower,"Vertex",id);}
        }
        public int Pick(Vector2 screen,string kind)
        {
            if(view==null||!BoardCamera.pixelRect.Contains(screen))return -1;
            float threshold=24*Mathf.Clamp(Screen.height/900f,.65f,2f); float best=threshold;int id=-1;
            if(kind=="Vertex")foreach(var v in view.Vertices){Vector2 p=BoardCamera.WorldToScreenPoint(positions[v.Id]+Vector3.up*.18f);float d=Vector2.Distance(screen,p);if(d<best){best=d;id=v.Id;}}
            else if(kind=="Edge")foreach(var e in view.Edges){Vector2 a=BoardCamera.WorldToScreenPoint(positions[e.A]),b=BoardCamera.WorldToScreenPoint(positions[e.B]);Vector2 delta=b-a;float t=Mathf.Clamp01(Vector2.Dot(screen-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));float d=Vector2.Distance(screen,a+delta*t);if(d<best){best=d;id=e.Id;}}
            else foreach(var hit in Physics.RaycastAll(BoardCamera.ScreenPointToRay(screen))){var t=hit.collider.GetComponent<BoardTarget>();if(t!=null&&t.Kind=="Hex")return t.Id;}
            return id;
        }
        private GameObject Primitive(PrimitiveType type,Vector3 position,Vector3 scale,Color color){var o=GameObject.CreatePrimitive(type);o.transform.SetParent(root);o.transform.position=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=Paint(color);return o;}
        private void Label(string text,Vector3 p,float size,Color color){var o=new GameObject("Label");o.transform.SetParent(root);o.transform.position=p;o.transform.rotation=Quaternion.Euler(90,0,0);var t=o.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");o.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;t.text=text;t.fontSize=40;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;}
        private static void Target(GameObject o,string kind,int id){var t=o.AddComponent<BoardTarget>();t.Kind=kind;t.Id=id;}
        private static Color TerrainColor(string r){switch(r){case "Brick":return new Color(.65f,.34f,.24f);case "Lumber":return new Color(.16f,.43f,.32f);case "Wool":return new Color(.41f,.62f,.35f);case "Grain":return new Color(.72f,.58f,.28f);case "Ore":return new Color(.38f,.44f,.54f);default:return new Color(.69f,.6f,.43f);}}
        private void OnDestroy(){foreach(var m in materials)Destroy(m);foreach(var m in meshes)Destroy(m);}
    }
}
