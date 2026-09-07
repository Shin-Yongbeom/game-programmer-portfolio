using System;
using Greyline.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.World
{
    /// <summary>Spatial orientation only. Places are reusable world data, never quest progression.</summary>
    public sealed class ProductionDistrictNavigation : MonoBehaviour
    {
        [Serializable]
        public struct Place
        {
            public string label;
            public Vector3 center;
            public Vector2 size;
            public Color color;
        }

        [SerializeField] private Transform player;
        [SerializeField] private Place[] places = Array.Empty<Place>();
        [SerializeField] private Vector2 districtMinimum = new(-83, -80);
        [SerializeField] private Vector2 districtMaximum = new(83, 86);
        private bool expandedMap;
        private GUIStyle titleStyle, smallStyle;
        public string CurrentPlace { get; private set; } = "RYUMYONG DISTRICT";
        public Place[] Places => places;
        public Transform Player => player;

        public void Configure(Transform target, Place[] locations)
        {
            player = target;
            places = locations ?? Array.Empty<Place>();
        }

        private void Update()
        {
            if (player == null) return;
            if (!Progression.ProductionSession.GameplayBlocked && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) expandedMap = !expandedMap;
            CurrentPlace = "RYUMYONG DISTRICT";
            foreach (Place place in places)
            {
                Vector3 d = player.position - place.center;
                if (Mathf.Abs(d.x) <= place.size.x * .5f && Mathf.Abs(d.z) <= place.size.y * .5f)
                {
                    CurrentPlace = place.label;
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (Progression.ProductionSession.Current?.MenuOpen == true) return;
            if (player == null) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            titleStyle.normal.textColor = ProductionUITokens.Text;
            smallStyle.normal.textColor = ProductionUITokens.Text;
            float scale = Mathf.Clamp(Screen.height / 900f, .75f, 1.5f);
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            Rect card = new(width - 292, 22, 270, 54);
            Fill(card, ProductionUITokens.Base);
            GUI.Label(new Rect(card.x + 12, card.y + 5, 250, 27), CurrentPlace, titleStyle);
            GUI.Label(new Rect(card.x + 12, card.y + 31, 250, 20), "M  DISTRICT MAP   ·   NORTH ↑", smallStyle);
            float mapSize = expandedMap ? 380 : 180;
            Rect map = new(width - mapSize - 22, 86, mapSize, mapSize);
            DrawMap(map);
            if (expandedMap)
            {
                Rect legend = new(map.x, map.yMax + 8, map.width, 108);
                Fill(legend, ProductionUITokens.Base);
                GUI.Label(new Rect(legend.x + 12, legend.y + 8, legend.width - 24, 96),
                    "BOULEVARD → UNIVERSITY → MARKET → PLAZA\nThe eastern repair hall connects the market alley to the north service lane.\nExplore freely. Combat pockets can be approached from multiple routes.", smallStyle);
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        private void DrawMap(Rect map)
        {
            Fill(map, ProductionUITokens.Base);
            // Roads correspond to the builder's unobstructed traversable network.
            MapRect(map, new Vector2(0, -14), new Vector2(23, 132), new Color(.22f, .25f, .25f));
            MapRect(map, new Vector2(0, -29), new Vector2(150, 11), new Color(.22f, .25f, .25f));
            MapRect(map, new Vector2(35, 24), new Vector2(80, 10), new Color(.22f, .25f, .25f));
            MapRect(map, new Vector2(63, 17), new Vector2(10, 99), new Color(.22f, .25f, .25f));
            foreach (Place place in places)
            {
                Color c = place.color; c.a = .72f;
                MapRect(map, new Vector2(place.center.x, place.center.z), place.size, c);
                Vector2 p = MapPoint(map, place.center);
                if (expandedMap) GUI.Label(new Rect(p.x - 48, p.y - 8, 110, 32), place.label, smallStyle);
            }
            Vector2 playerPoint = MapPoint(map, player.position);
            Vector3 look = player.forward;
            Vector2 heading = new(look.x, -look.z);
            Fill(new Rect(playerPoint.x - 4, playerPoint.y - 4, 8, 8), Color.white);
            Fill(new Rect(playerPoint.x + heading.x * 9 - 2, playerPoint.y + heading.y * 9 - 2, 4, 4), new Color(.9f, .72f, .36f));
        }

        private void MapRect(Rect map, Vector2 center, Vector2 size, Color color)
        {
            Vector2 p = MapPoint(map, new Vector3(center.x - size.x * .5f, 0, center.y + size.y * .5f));
            Fill(new Rect(p.x, p.y, size.x / (districtMaximum.x - districtMinimum.x) * map.width,
                size.y / (districtMaximum.y - districtMinimum.y) * map.height), color);
        }

        private Vector2 MapPoint(Rect map, Vector3 point) => new(
            map.x + Mathf.InverseLerp(districtMinimum.x, districtMaximum.x, point.x) * map.width,
            map.yMax - Mathf.InverseLerp(districtMinimum.y, districtMaximum.y, point.z) * map.height);

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
