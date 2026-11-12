using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Повесьте на любой объект (например, на куб). Пока объект двигается — вручную в Scene View
/// или из кода — от него к осям рисуются линии-проекции и подписываются его координаты.
/// Всё считается в МИРОВЫХ координатах, то есть относительно системы WorldAxes в (0,0,0).
///
/// 2D-режим (show3D = false): две линии — до оси X и до оси Y.
/// 3D-режим (show3D = true): «коробка» из линий от объекта до осей X, Y, Z.
///
/// Из кода:  cube.AddComponent&lt;AxesTracker&gt;();
/// </summary>
public class AxesTracker : MonoBehaviour
{
    [Header("Режим")]
    public bool show3D = false;

    [Header("Линии")]
    public float lineWidth = 0.03f;
    public bool dashed = true;            // пунктир
    public float dashLength = 0.2f;       // длина штриха (и промежутка)
    public int sortingOrder = 1001;
    public Color xColor = new Color(1f, 0.25f, 0.25f);
    public Color yColor = new Color(0.3f, 1f, 0.3f);
    public Color zColor = new Color(0.35f, 0.5f, 1f);
    public Color planeColor = new Color(1f, 1f, 1f, 0.5f);   // линии до плоскостей (только 3D)

    [Header("Подписи")]
    public bool drawLabels = true;
    public bool showPointLabel = true;    // "(x, y)" рядом с объектом
    public bool showFootLabels = true;    // значения на самих осях
    public int decimals = 2;              // знаков после запятой
    public float labelSize = 0.3f;
    public Vector3 pointLabelOffset = new Vector3(0f, 0.7f, 0f);
    public float footLabelOffset = 0.8f;  // отступ подписи от оси

    GameObject _root;
    LineRenderer[] _lines;
    Color[] _colors;
    TextMesh _pointLabel, _footX, _footY, _footZ;
    readonly List<Transform> _labelTransforms = new List<Transform>();
    readonly List<TextMesh> _labelMeshes = new List<TextMesh>();
    Vector3 _lastPointOffset;
    float _lastFootOffset;
    int _lastDecimals;
    Material _plainMat, _dashMat;
    Font _font;
    Vector3 _last;
    bool _inited;

    static Texture2D _dashTex;

    // ------------------------------------------------------------ жизненный цикл

    void Start()
    {
        _font = LoadBuiltinFont();
        _root = new GameObject("AxesTracker_" + name);   // не дочерний: не наследует поворот/масштаб объекта

        _plainMat = new Material(Shader.Find("Sprites/Default"));
        _dashMat = new Material(Shader.Find("Sprites/Default"));
        _dashMat.mainTexture = GetDashTexture();
        // одна «точка + пробел» на 2*dashLength мировых единиц
        _dashMat.mainTextureScale = new Vector2(1f / (2f * Mathf.Max(0.001f, dashLength)), 1f);

        BuildLines();
        if (drawLabels) BuildLabels();
    }

    void OnEnable() { if (_root != null) _root.SetActive(true); }
    void OnDisable() { if (_root != null) _root.SetActive(false); }
    void OnDestroy() { if (_root != null) Destroy(_root); }

    void LateUpdate()
    {
        if (_root == null) return;

        Vector3 p = transform.position;

        // Настройки подписей можно менять прямо во время игры
        bool cfgChanged = pointLabelOffset != _lastPointOffset
                          || !Mathf.Approximately(footLabelOffset, _lastFootOffset)
                          || decimals != _lastDecimals;
        if (cfgChanged)
        {
            _lastPointOffset = pointLabelOffset;
            _lastFootOffset = footLabelOffset;
            _lastDecimals = decimals;
        }

        if (!_inited || cfgChanged || p != _last)
        {
            _inited = true;
            _last = p;
            Refresh(p);
        }

        // Размер цифр применяется каждый кадр, так что Label Size меняется «вживую»
        float cs = labelSize * 0.1f;
        for (int i = 0; i < _labelMeshes.Count; i++)
            if (_labelMeshes[i].characterSize != cs) _labelMeshes[i].characterSize = cs;

        // Подписи всегда повёрнуты к камере
        var cam = Camera.main;
        if (cam != null)
        {
            Quaternion rot = cam.transform.rotation;
            for (int i = 0; i < _labelTransforms.Count; i++)
                _labelTransforms[i].rotation = rot;
        }
    }

    // ------------------------------------------------------------ построение

    void BuildLines()
    {
        int count = show3D ? 9 : 2;
        _lines = new LineRenderer[count];
        _colors = show3D
            ? new[] { planeColor, planeColor, planeColor, xColor, yColor, xColor, zColor, yColor, zColor }
            : new[] { xColor, yColor };

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("line" + i);
            go.transform.SetParent(_root.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = lineWidth;
            lr.startColor = lr.endColor = _colors[i];
            lr.sharedMaterial = dashed ? _dashMat : _plainMat;
            lr.textureMode = LineTextureMode.Tile;
            lr.sortingOrder = sortingOrder;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _lines[i] = lr;
        }
    }

    void BuildLabels()
    {
        if (_font == null) return;
        if (showPointLabel) _pointLabel = MakeLabel("point", Color.white);
        if (showFootLabels)
        {
            _footX = MakeLabel("footX", xColor);
            _footY = MakeLabel("footY", yColor);
            if (show3D) _footZ = MakeLabel("footZ", zColor);
        }
    }

    TextMesh MakeLabel(string labelName, Color color)
    {
        var go = new GameObject("label_" + labelName);
        go.transform.SetParent(_root.transform, false);

        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        tm.fontSize = 64;
        tm.characterSize = labelSize * 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _font.material;
        mr.sortingOrder = sortingOrder + 1;

        _labelTransforms.Add(go.transform);
        _labelMeshes.Add(tm);
        return tm;
    }

    // ------------------------------------------------------------ обновление

    void Refresh(Vector3 p)
    {
        if (!show3D) p.z = 0f;   // 2D: работаем в плоскости XY

        float x = p.x, y = p.y, z = p.z;

        if (!show3D)
        {
            SetLine(0, p, new Vector3(x, 0, 0));   // до оси X
            SetLine(1, p, new Vector3(0, y, 0));   // до оси Y
        }
        else
        {
            var pxy = new Vector3(x, y, 0);
            var pxz = new Vector3(x, 0, z);
            var pyz = new Vector3(0, y, z);
            SetLine(0, p, pxy);
            SetLine(1, p, pxz);
            SetLine(2, p, pyz);
            SetLine(3, pxy, new Vector3(x, 0, 0));
            SetLine(4, pxy, new Vector3(0, y, 0));
            SetLine(5, pxz, new Vector3(x, 0, 0));
            SetLine(6, pxz, new Vector3(0, 0, z));
            SetLine(7, pyz, new Vector3(0, y, 0));
            SetLine(8, pyz, new Vector3(0, 0, z));
        }

        if (_pointLabel != null)
        {
            _pointLabel.text = show3D
                ? "(" + F(x) + ", " + F(y) + ", " + F(z) + ")"
                : "(" + F(x) + ", " + F(y) + ")";
            _pointLabel.transform.position = p + pointLabelOffset;
        }
        if (_footX != null)
        {
            _footX.text = F(x);
            _footX.transform.position = new Vector3(x, -footLabelOffset, 0);
        }
        if (_footY != null)
        {
            _footY.text = F(y);
            _footY.transform.position = new Vector3(-footLabelOffset, y, 0);
        }
        if (_footZ != null)
        {
            _footZ.text = F(z);
            _footZ.transform.position = new Vector3(0, -footLabelOffset, z);
        }
    }

    void SetLine(int i, Vector3 a, Vector3 b)
    {
        _lines[i].SetPosition(0, a);
        _lines[i].SetPosition(1, b);
    }

    string F(float v)
    {
        float k = Mathf.Pow(10f, Mathf.Max(0, decimals));
        v = Mathf.Round(v * k) / k + 0f;                  // + 0f убирает «-0»
        return v.ToString("0." + new string('#', Mathf.Max(0, decimals)), CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------ ресурсы

    static Texture2D GetDashTexture()
    {
        if (_dashTex != null) return _dashTex;
        _dashTex = new Texture2D(2, 1, TextureFormat.RGBA32, false);
        _dashTex.SetPixel(0, 0, Color.white);                       // штрих
        _dashTex.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));         // промежуток
        _dashTex.filterMode = FilterMode.Point;
        _dashTex.wrapMode = TextureWrapMode.Repeat;
        _dashTex.Apply();
        return _dashTex;
    }

    static Font LoadBuiltinFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }
}