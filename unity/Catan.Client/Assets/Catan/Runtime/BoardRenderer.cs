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
        private readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
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
            BoardCamera=obj.AddComponent<Camera>(); BoardCamera.orthographic=true; BoardCamera.orthographicSize=5.1f;
            BoardCamera.transform.position=new Vector3(0,18,-1); BoardCamera.transform.LookAt(Vector3.zero);
            BoardCamera.depth=10; BoardCamera.clearFlags=CameraClearFlags.SolidColor; BoardCamera.backgroundColor=new Color(.07f,.17f,.22f);
        }
        public void SetViewport(Rect pixels)
        {
            BoardCamera.rect=new Rect(pixels.x/Screen.width,1-(pixels.y+pixels.height)/Screen.height,pixels.width/Screen.width,pixels.height/Screen.height);
            // Fit both coast dimensions when the game window is narrow.
            BoardCamera.orthographicSize=Mathf.Max(5.1f,5.0f/Mathf.Max(.1f,pixels.width/pixels.height));
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
            foreach(var m in materials)Destroy(m);materials.Clear();foreach(var m in meshes)Destroy(m);meshes.Clear();positions.Clear();foreach(var sprite in sprites)Destroy(sprite);sprites.Clear();
            root=new GameObject("Board").transform;root.SetParent(transform);
            Primitive(PrimitiveType.Cylinder,new Vector3(0,-.22f,0),new Vector3(9.1f,.16f,9.1f),new Color(.13f,.36f,.44f));
            Primitive(PrimitiveType.Cylinder,new Vector3(0,-.08f,0),new Vector3(8.45f,.045f,8.45f),new Color(.78f,.68f,.45f));
            foreach(var h in view.Hexes)
            {
                Vector3 center=new Vector3(Mathf.Sqrt(3)*(h.Q+h.R/2f),0,1.5f*h.R);
                var verts=new Vector3[7]; for(int k=0;k<6;k++){float a=(60*k-30)*Mathf.Deg2Rad;Vector3 c=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));verts[k+1]=c*.96f;positions[h.Vertices[k]]=center+c;}
                var triangles=new int[18];for(int k=0;k<6;k++){triangles[k*3]=0;triangles[k*3+1]=(k+1)%6+1;triangles[k*3+2]=k+1;}
                var mesh=new Mesh{vertices=verts,triangles=triangles};mesh.RecalculateNormals();meshes.Add(mesh);
                var obj=new GameObject("Terrain");obj.transform.SetParent(root);obj.transform.position=center;
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=Paint(TerrainColor(h.Resource));obj.AddComponent<MeshCollider>().sharedMesh=mesh;Target(obj,"Hex",h.Id);
                TerrainDetails(h,center);
                if(h.Resource!=null)Icon(h.Resource,center+new Vector3(-.39f,.10f,.24f),.34f,Color.white);
                Label(h.Resource??"Desert",center+new Vector3(.05f,.08f,.34f),.045f,Color.white);
                if(h.Number>0)
                {
                    Primitive(PrimitiveType.Cylinder,center+new Vector3(0,.025f,-.13f),new Vector3(.50f,.018f,.50f),new Color(.95f,.88f,.69f));
                    Color ink=h.Number==6||h.Number==8?new Color(.72f,.16f,.12f):new Color(.18f,.21f,.23f);
                    Label(h.Number.ToString(),center+new Vector3(0,.065f,-.09f),.09f,ink);
                    int dots=6-Math.Abs(7-h.Number);Label(new string('•',dots),center+new Vector3(0,.065f,-.30f),.026f,ink);
                }
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
            foreach(var p in view.Ports)Port(p);
        }
        private void Icon(string name,Vector3 position,float size,Color color)
        {
            if(!icons.TryGetValue(name,out var texture)){texture=TabletopSkin.CreateIcon(name);icons.Add(name,texture);}
            var obj=new GameObject(name+" icon");obj.transform.SetParent(root);obj.transform.position=position;obj.transform.rotation=Quaternion.Euler(90,0,0);
            var sprite=obj.AddComponent<SpriteRenderer>();sprite.sprite=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),64/size);sprite.color=color;
            // Sprite lifetime matches the board render; textures are cached until shutdown.
            sprites.Add(sprite.sprite);
        }
        private readonly List<Sprite> sprites=new List<Sprite>();
        private void TerrainDetails(Terrain h,Vector3 center)
        {
            for(int i=0;i<3;i++)
            {
                Vector3 p=center+new Vector3(-.46f+i*.44f,.08f,-.58f);
                if(h.Resource=="Lumber")
                {
                    Primitive(PrimitiveType.Cylinder,p,new Vector3(.06f,.09f,.06f),new Color(.38f,.23f,.13f));
                    Primitive(PrimitiveType.Sphere,p+Vector3.up*.14f,new Vector3(.24f,.32f,.24f),new Color(.11f,.32f,.20f));
                }
                else if(h.Resource=="Ore")Primitive(PrimitiveType.Cube,p,new Vector3(.22f,.12f+i*.03f,.18f),new Color(.55f,.62f,.68f)).transform.rotation=Quaternion.Euler(0,25+i*31,15);
                else if(h.Resource=="Grain")Primitive(PrimitiveType.Cube,p,new Vector3(.26f,.025f,.12f),new Color(.93f,.75f,.32f));
                else if(h.Resource=="Wool")
                {
                    Primitive(PrimitiveType.Sphere,p,new Vector3(.22f,.12f,.16f),new Color(.94f,.94f,.83f));
                    Primitive(PrimitiveType.Sphere,p+new Vector3(.11f,0,0),Vector3.one*.08f,new Color(.25f,.28f,.22f));
                }
                else if(h.Resource=="Brick")Primitive(PrimitiveType.Cube,p,new Vector3(.24f,.08f,.15f),new Color(.84f,.43f,.29f));
                else Primitive(PrimitiveType.Sphere,p,new Vector3(.36f,.025f,.16f),new Color(.88f,.75f,.47f));
            }
        }
        private void Port(Harbor port)
        {
            Vector3 coast=(positions[port.A]+positions[port.B])/2;Vector3 outward=coast.normalized;
            Vector3 station=coast+outward*.68f;
            Color owner=PlayerColor(view,view.Vertices.FirstOrDefault(v=>(v.Id==port.A||v.Id==port.B)&&v.Level>0)?.Owner);
            foreach(int id in new[]{port.A,port.B})
            {
                Vector3 end=station-outward*.24f;Vector3 delta=end-positions[id];
                var pier=Primitive(PrimitiveType.Cube,(end+positions[id])/2+Vector3.up*.02f,new Vector3(.10f,.07f,delta.magnitude),new Color(.56f,.38f,.22f));pier.transform.rotation=Quaternion.LookRotation(delta);
            }
            Primitive(PrimitiveType.Cylinder,station+Vector3.up*.035f,new Vector3(.72f,.045f,.72f),new Color(.91f,.84f,.66f));
            Icon(port.Resource??"Trade",station+new Vector3(-.14f,.10f,0),.23f,port.Resource==null?new Color(.25f,.39f,.43f):TerrainColor(port.Resource));
            Label(port.Resource==null?"3:1":"2:1",station+new Vector3(.15f,.12f,0),.055f,new Color(.16f,.23f,.25f));
            Label(port.Resource??"Any resource",station+new Vector3(0,.12f,-.28f),.029f,new Color(.95f,.94f,.82f));
            Primitive(PrimitiveType.Cube,station+new Vector3(0,.09f,.31f),new Vector3(.48f,.06f,.09f),owner);
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
        private void OnDestroy(){foreach(var texture in icons.Values)Destroy(texture);foreach(var sprite in sprites)Destroy(sprite);foreach(var m in materials)Destroy(m);foreach(var m in meshes)Destroy(m);}
    }
}
