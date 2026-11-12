using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рисует декартову систему координат от МИРОВЫХ координат (0,0,0) при запуске сцены.
/// Ничего никуда вешать не нужно: объект создаётся автоматически после загрузки сцены.
/// Если хотите менять настройки в Inspector — положите компонент на любой объект сцены,
/// тогда автосоздание не сработает и будет использован ваш.
/// </summary>
public class WorldAxes : MonoBehaviour
{
    [Header("Размеры")]
    public float halfLength = 10f;     // длина оси в каждую сторону от (0,0,0)
    public float step = 1f;            // шаг делений и сетки
    public float tickSize = 0.12f;     // размер штриха
    public float arrowSize = 0.5f;     // размер стрелки

    [Header("Что рисовать")]
    public bool show3D = false;        // ось Z
    public bool drawGrid = true;       // сетка в плоскости XY
    public bool drawTicks = true;
    public bool drawLabels = true;

    [Header("Вид")]
    public float lineWidth = 0.04f;
    public float gridWidth = 0.015f;
    [Tooltip("Размер цифр на делениях (и нуля)")]
    public float numberSize = 1f;
    [Tooltip("Размер букв названий осей X, Y, Z")]
    public float labelSize = 0.4f;
    public int sortingOrder = 1000;    // поверх спрайтов
    public Color xColor = new Color(1f, 0.25f, 0.25f);
    public Color yColor = new Color(0.3f, 1f, 0.3f);
    public Color zColor = new Color(0.35f, 0.5f, 1f);
    public Color gridColor = new Color(1f, 1f, 1f, 0.2f);

    Material _mat;
    Font _font;
    readonly List<Transform> _labels = new List<Transform>();

    // ------------------------------------------------------------ автосоздание

//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//    static void AutoCreate()
//    {
//#if UNITY_2023_1_OR_NEWER
//        if (FindFirstObjectByType<WorldAxes>() != null) return;
//#else
//        if (FindObjectOfType<WorldAxes>() != null) return;
//#endif
//        new GameObject("WorldAxes").AddComponent<WorldAxes>();
//    }

    // ------------------------------------------------------------ построение

    void Start()
    {
        // Всегда строим от мировых координат, независимо от положения этого объекта.
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        _mat = new Material(Shader.Find("Sprites/Default"));
        _font = LoadBuiltinFont();

        if (step <= 0f || halfLength <= 0f) return;
        int n = Mathf.FloorToInt(halfLength / step);

        // Сетка
        if (drawGrid)
        {
            for (int i = -n; i <= n; i++)
            {
                if (i == 0) continue;
                float p = i * step;
                MakeLine("grid", gridColor, gridWidth, new Vector3(p, -halfLength, 0), new Vector3(p, halfLength, 0));
                MakeLine("grid", gridColor, gridWidth, new Vector3(-halfLength, p, 0), new Vector3(halfLength, p, 0));
            }
        }

        // Оси
        BuildAxis("X", Vector3.right, Vector3.up, Vector3.forward, xColor);
        BuildAxis("Y", Vector3.up, Vector3.right, Vector3.forward, yColor);
        if (show3D) BuildAxis("Z", Vector3.forward, Vector3.up, Vector3.right, zColor);

        // Деления и подписи
        for (int i = -n; i <= n; i++)
        {
            if (i == 0) continue;
            float p = i * step;
            string text = p.ToString("0.##");

            if (drawTicks)
            {
                MakeLine("tickX", xColor, lineWidth * 0.6f, new Vector3(p, -tickSize, 0), new Vector3(p, tickSize, 0));
                MakeLine("tickY", yColor, lineWidth * 0.6f, new Vector3(-tickSize, p, 0), new Vector3(tickSize, p, 0));
                if (show3D)
                    MakeLine("tickZ", zColor, lineWidth * 0.6f, new Vector3(0, -tickSize, p), new Vector3(0, tickSize, p));
            }
            if (drawLabels)
            {
                MakeLabel(text, new Vector3(p, -tickSize * 3f, 0), xColor, numberSize);
                MakeLabel(text, new Vector3(-tickSize * 3f, p, 0), yColor, numberSize);
                if (show3D) MakeLabel(text, new Vector3(0, -tickSize * 3f, p), zColor, numberSize);
            }
        }

        if (drawLabels)
        {
            MakeLabel("0", new Vector3(-tickSize * 3f, -tickSize * 3f, 0), Color.white, numberSize);
            float e = halfLength + arrowSize * 1.5f;
            MakeLabel("X", Vector3.right * e, xColor, labelSize);
            MakeLabel("Y", Vector3.up * e, yColor, labelSize);
            if (show3D) MakeLabel("Z", Vector3.forward * e, zColor, labelSize);
        }
    }

    void BuildAxis(string axisName, Vector3 dir, Vector3 side1, Vector3 side2, Color color)
    {
        Vector3 end = dir * halfLength;
        MakeLine(axisName, color, lineWidth, -end, end);

        // Стрелка на положительном конце: «ёлочка» в двух плоскостях
        Vector3 back = end - dir * arrowSize;
        float w = arrowSize * 0.3f;
        MakeLine(axisName + "_arrow", color, lineWidth, back + side1 * w, end, back - side1 * w);
        if (show3D)
            MakeLine(axisName + "_arrow", color, lineWidth, back + side2 * w, end, back - side2 * w);
    }

    void MakeLine(string lineName, Color color, float width, params Vector3[] points)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;                  // мировые координаты
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = color;
        lr.sharedMaterial = _mat;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    void MakeLabel(string text, Vector3 worldPos, Color color, float size)
    {
        if (_font == null) return;

        var go = new GameObject("label_" + text);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = _font;
        tm.fontSize = 64;
        tm.characterSize = size * 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _font.material;
        mr.sortingOrder = sortingOrder + 1;

        _labels.Add(go.transform);
    }

    // Подписи всегда развёрнуты к камере (удобно и в 2D, и в 3D)
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Quaternion rot = cam.transform.rotation;
        for (int i = 0; i < _labels.Count; i++)
            _labels[i].rotation = rot;
    }

    static Font LoadBuiltinFont()
    {
        // Название встроенного шрифта зависит от версии Unity
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }
}