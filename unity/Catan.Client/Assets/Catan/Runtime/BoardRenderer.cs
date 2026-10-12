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
        private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Color[] playerColors = { new Color(.9f,.28f,.2f), new Color(.25f,.5f,.95f), new Color(.97f,.77f,.18f), new Color(.85f,.85f,.85f) };
        public Camera BoardCamera { get; private set; }
        private void Awake()
        {
            var cameraObject = new GameObject("Board camera"); cameraObject.transform.SetParent(transform);
            BoardCamera = cameraObject.AddComponent<Camera>(); BoardCamera.orthographic = true; BoardCamera.orthographicSize = 5.7f;
            BoardCamera.transform.position = new Vector3(0, 18, -3); BoardCamera.transform.LookAt(Vector3.zero);
            BoardCamera.depth = 10;
            BoardCamera.clearFlags = CameraClearFlags.SolidColor; BoardCamera.backgroundColor = new Color(.035f,.08f,.12f);
            BoardCamera.rect = new Rect(.28f,.14f,.52f,.72f);
        }
        private Material Material(Color color)
        {
            var material = new Material(Resources.Load<Shader>("Board")) { color = color }; materials.Add(material); return material;
        }
        private Color Owner(Snapshot view, string id) => id == null ? new Color(.35f,.42f,.45f) : playerColors[Math.Max(0, Array.FindIndex(view.Players, p => p.Id == id)) % playerColors.Length];
        public void Render(HostResponse response)
        {
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            foreach (var m in materials) Destroy(m); materials.Clear(); foreach (var m in meshes) Destroy(m); meshes.Clear(); positions.Clear();
            root = new GameObject("Logical board projection").transform; root.SetParent(transform);
            var view = response.View;
            foreach (var hex in view.Hexes)
            {
                Vector3 center = new Vector3(Mathf.Sqrt(3) * (hex.Q + hex.R / 2f), 0, 1.5f * hex.R);
                var vertices = new Vector3[7]; vertices[0] = Vector3.zero;
                for (int k = 0; k < 6; k++)
                {
                    float angle = (60 * k - 30) * Mathf.Deg2Rad;
                    Vector3 corner = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    vertices[k + 1] = corner * .95f; positions[hex.Vertices[k]] = center + corner;
                }
                var triangles = new int[18]; for (int k = 0; k < 6; k++) { triangles[k * 3] = 0; triangles[k * 3 + 1] = (k + 1) % 6 + 1; triangles[k * 3 + 2] = k + 1; }
                var mesh = new Mesh { vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); meshes.Add(mesh);
                var obj = new GameObject("Hex " + hex.Id); obj.transform.SetParent(root); obj.transform.position = center;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = Material(TerrainColor(hex.Resource));
                obj.AddComponent<MeshCollider>().sharedMesh = mesh; Target(obj, "Hex", hex.Id);
                Label("H" + hex.Id + "\n" + (hex.Resource ?? "Desert") + " " + (hex.Number == 0 ? "" : hex.Number.ToString()), center + new Vector3(0,.05f,0), .055f);
                if (hex.Id == view.RobberHex) { var robber = Primitive(PrimitiveType.Capsule, center + new Vector3(.45f,.28f,.1f), new Vector3(.16f,.25f,.16f), Color.black); Target(robber, "Hex", hex.Id); }
            }
            foreach (var edge in view.Edges)
            {
                Vector3 a = positions[edge.A], b = positions[edge.B];
                bool legal = response.LegalRoads != null && response.LegalRoads.Contains(edge.Id);
                Color color = edge.Owner != null ? Owner(view, edge.Owner) : legal ? new Color(.3f,.8f,.75f) : new Color(.22f,.27f,.3f);
                var road = Primitive(PrimitiveType.Cube, (a+b)/2 + Vector3.up * .07f, new Vector3(edge.Owner == null ? .07f : .13f, .10f, Vector3.Distance(a,b) * .78f), color);
                road.transform.rotation = Quaternion.LookRotation(b-a); Target(road, "Edge", edge.Id);
                Label(edge.Id.ToString(), (a+b)/2 + Vector3.up * .14f, .032f);
            }
            foreach (var vertex in view.Vertices)
            {
                bool legal = response.LegalSettlements != null && response.LegalSettlements.Contains(vertex.Id);
                var scale = vertex.Level == 2 ? new Vector3(.26f,.28f,.26f) : vertex.Level == 1 ? new Vector3(.2f,.2f,.2f) : Vector3.one * .12f;
                var piece = Primitive(vertex.Level > 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, positions[vertex.Id] + Vector3.up * .16f, scale,
                    vertex.Owner != null ? Owner(view, vertex.Owner) : legal ? Color.cyan : new Color(.65f,.72f,.76f));
                Target(piece, "Vertex", vertex.Id); Label(vertex.Id.ToString(), positions[vertex.Id] + Vector3.up * .36f, .035f);
            }
            foreach (var port in view.Ports) Label((port.Resource ?? "Any") + " port", (positions[port.A]+positions[port.B])/2 * 1.14f + Vector3.up * .06f, .037f);
        }
        private GameObject Primitive(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(type); obj.transform.SetParent(root); obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Material(color); return obj;
        }
        private void Label(string text, Vector3 position, float size)
        {
            var obj = new GameObject("Label"); obj.transform.SetParent(root); obj.transform.position = position; obj.transform.rotation = Quaternion.Euler(90,0,0);
            var label = obj.AddComponent<TextMesh>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            obj.GetComponent<MeshRenderer>().sharedMaterial = label.font.material; label.text = text; label.fontSize = 40; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = Color.white;
        }
        private static void Target(GameObject obj, string kind, int id) { var target = obj.AddComponent<BoardTarget>(); target.Kind = kind; target.Id = id; }
        private static Color TerrainColor(string resource)
        {
            switch (resource)
            {
                case "Brick": return new Color(.63f,.28f,.18f);
                case "Lumber": return new Color(.12f,.4f,.22f);
                case "Wool": return new Color(.45f,.64f,.27f);
                case "Grain": return new Color(.72f,.58f,.2f);
                case "Ore": return new Color(.34f,.39f,.47f);
                default: return new Color(.7f,.6f,.4f);
            }
        }
        private void OnDestroy() { foreach (var m in materials) Destroy(m); foreach (var m in meshes) Destroy(m); }
    }
}
