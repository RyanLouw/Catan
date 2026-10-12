using System;
using System.Collections.Generic;
using UnityEngine;
namespace Catan.Client
{
    public sealed class TabletopSkin : IDisposable
    {
        public readonly Color Background=new Color(.055f,.10f,.13f),Panel=new Color(.09f,.16f,.19f),Gold=new Color(.96f,.77f,.39f),Ink=new Color(.88f,.94f,.92f),Muted=new Color(.56f,.68f,.68f);
        private readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
        private GUIStyle text,button,field;
        private readonly Texture2D dial=new Texture2D(128,128,TextureFormat.RGBA32,false);
        private int lastDial=-1;
        public void Box(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        public void Text(Rect rect,string value,int size=20,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            if(text==null)text=new GUIStyle(GUI.skin.label);text.fontSize=size;text.normal.textColor=color??Ink;text.alignment=align;text.wordWrap=true;GUI.Label(rect,value,text);
        }
        public bool Button(Rect rect,string label,bool enabled=true,bool primary=false)
        {
            if(button==null)button=new GUIStyle(GUI.skin.button){fontSize=20,alignment=TextAnchor.MiddleCenter};
            bool prior=GUI.enabled;GUI.enabled=prior&&enabled;var old=GUI.backgroundColor;GUI.backgroundColor=primary?Gold:new Color(.25f,.41f,.43f);
            bool clicked=GUI.Button(rect,label,button);GUI.backgroundColor=old;GUI.enabled=prior;return clicked;
        }
        public string Field(Rect rect,string value){if(field==null)field=new GUIStyle(GUI.skin.textField){fontSize=22};return GUI.TextField(rect,value,field);}
        public void Icon(Rect rect,string name,Color? color=null)
        {
            if(!icons.TryGetValue(name,out var texture)){texture=CreateIcon(name);icons.Add(name,texture);}
            var old=GUI.color;GUI.color=color??Ink;GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit);GUI.color=old;
        }
        public void Timer(Rect rect,double remaining,double total)
        {
            int tick=(int)Math.Min(100,Math.Max(0,remaining/Math.Max(1,total)*100));
            var accent=remaining<=10?new Color(1,.4f,.35f):remaining<=30?Gold:new Color(.4f,.88f,.69f);
            if(lastDial!=tick)
            {
                lastDial=tick;var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float dx=x-63.5f,dy=y-63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);if(r>50&&r<62){float a=(Mathf.Atan2(dx,dy)+Mathf.PI*2)%(Mathf.PI*2);pixels[y*128+x]=a<=tick/100f*Mathf.PI*2?accent:new Color(.19f,.3f,.32f);}}
                dial.SetPixels(pixels);dial.Apply();
            }
            GUI.DrawTexture(rect,dial);Text(rect,Math.Ceiling(remaining).ToString("0"),29,accent,TextAnchor.MiddleCenter);
        }
        public void Die(Rect rect,int value,bool rolling=false)
        {
            Box(rect,new Color(.95f,.95f,.9f));
            float pip=rect.width*.12f;
            Vector2[] points;
            switch(value)
            {
                case 1:points=new[]{new Vector2(.5f,.5f)};break;
                case 2:points=new[]{new Vector2(.28f,.28f),new Vector2(.72f,.72f)};break;
                case 3:points=new[]{new Vector2(.28f,.28f),new Vector2(.5f,.5f),new Vector2(.72f,.72f)};break;
                case 4:points=new[]{new Vector2(.28f,.28f),new Vector2(.72f,.28f),new Vector2(.28f,.72f),new Vector2(.72f,.72f)};break;
                case 5:points=new[]{new Vector2(.28f,.28f),new Vector2(.72f,.28f),new Vector2(.5f,.5f),new Vector2(.28f,.72f),new Vector2(.72f,.72f)};break;
                default:points=new[]{new Vector2(.28f,.25f),new Vector2(.72f,.25f),new Vector2(.28f,.5f),new Vector2(.72f,.5f),new Vector2(.28f,.75f),new Vector2(.72f,.75f)};break;
            }
            foreach(var point in points)Box(new Rect(rect.x+point.x*rect.width-pip/2,rect.y+point.y*rect.height-pip/2,pip,pip),rolling?Muted:Background);
        }
        private static Texture2D CreateIcon(string kind)
        {
            var t=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};var colors=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=x/63f,v=y/63f;bool fill=false;
                switch(kind)
                {
                    case "Road":fill=Math.Abs(v-u*.65f-.15f)<.095f&&u>.1f&&u<.9f;break;
                    case "Settlement":fill=u>.23f&&u<.77f&&v>.12f&&v<.53f||v>=.53f&&v<.87f&&Math.Abs(u-.5f)<(.87f-v)*1.05f;break;
                    case "City":fill=u>.12f&&u<.86f&&v>.12f&&v<.5f||u>.17f&&u<.4f&&v>=.5f&&v<.8f||u>.58f&&u<.81f&&v>=.5f&&v<.9f;break;
                    case "Robber":fill=Math.Pow((u-.5f)/.17f,2)+Math.Pow((v-.75f)/.17f,2)<1||v>.12f&&v<.6f&&Math.Abs(u-.5f)<.14f+( .6f-v)*.22f;break;
                    case "Dice":fill=u>.12f&&u<.88f&&v>.12f&&v<.88f; if(fill&&(Circle(u,v,.28f,.28f,.055f)||Circle(u,v,.5f,.5f,.055f)||Circle(u,v,.72f,.72f,.055f)))fill=false;break;
                    case "Brick":fill=u>.1f&&u<.9f&&v>.22f&&v<.75f&&Math.Abs(v-.49f)>.025f&&!(v<.49f&&Math.Abs(u-.5f)<.025f)&&!(v>.49f&&(Math.Abs(u-.28f)<.02f||Math.Abs(u-.7f)<.02f));break;
                    case "Lumber":fill=u>.45f&&u<.55f&&v>.08f&&v<.32f||v>.27f&&v<.9f&&Math.Abs(u-.5f)<(.95f-v)*.5f;break;
                    case "Wool":fill=Circle(u,v,.3f,.5f,.19f)||Circle(u,v,.5f,.58f,.25f)||Circle(u,v,.72f,.5f,.19f)||u>.28f&&u<.38f&&v>.13f&&v<.37f||u>.64f&&u<.74f&&v>.13f&&v<.37f;break;
                    case "Grain":fill=Math.Abs(u-.5f)<.025f&&v>.1f&&v<.9f||Math.Abs(Math.Abs(u-.5f)-.13f)<.09f&&(Circle(u,v,.36f,.4f,.1f)||Circle(u,v,.64f,.55f,.1f)||Circle(u,v,.36f,.7f,.1f)||Circle(u,v,.64f,.8f,.1f));break;
                    case "Ore":fill=v>.18f&&v<.8f&&Math.Abs(u-.5f)<(v<.55f? .35f:.35f-(v-.55f)*.8f);break;
                    case "Trade":fill=Math.Abs(v-.35f)<.05f&&u>.16f&&u<.8f||Math.Abs(v-.65f)<.05f&&u>.2f&&u<.84f||u>.7f&&u<.9f&&Math.Abs(v-.35f)<(.9f-u)*.8f||u>.1f&&u<.3f&&Math.Abs(v-.65f)<(u-.1f)*.8f;break;
                    default:fill=u>.2f&&u<.8f&&v>.1f&&v<.9f;break;
                }
                colors[y*64+x]=fill?Color.white:Color.clear;
            }
            t.SetPixels(colors);t.Apply();return t;
        }
        private static bool Circle(float x,float y,float cx,float cy,float r)=>Math.Pow(x-cx,2)+Math.Pow(y-cy,2)<r*r;
        public void Dispose(){foreach(var i in icons.Values)UnityEngine.Object.Destroy(i);UnityEngine.Object.Destroy(dial);}
    }
}
