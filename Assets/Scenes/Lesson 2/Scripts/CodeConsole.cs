using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Внутриигровая консоль: поле ввода на Canvas, куда можно писать C#-команды вида
///     transform.DOMove(new Vector3(3, 0, 0), 1).SetEase(Ease.OutBounce)
///     transform.position = new Vector3(0, 2, 0)
///     GameObject.Find("Cube").transform.Translate(1, 0, 0)
/// и они выполняются при нажатии Enter (или кнопки Run).
///
/// Как использовать: повесьте компонент на любой объект сцены, перетащите в поле Target
/// объект (например, куб) и нажмите Play. Canvas и поле ввода создаются автоматически.
/// Можно назначить и свои Input Field / Text / Button (обычные UI, не TMP).
/// </summary>
public class CodeConsole : MonoBehaviour
{
    [Header("Цель команд (transform, gameObject, target)")]
    public GameObject target;

    [Header("Свой UI (необязательно — иначе создастся автоматически)")]
    public InputField inputField;
    public Text outputText;
    public Button runButton;

    [Header("Вид")]
    public int fontSize = 22;
    public int maxLines = 12;
    public bool showUnityLogs = true;   // показывать Debug.Log в консоли

    readonly MiniEval _eval = new MiniEval();
    readonly List<string> _log = new List<string>();
    readonly List<string> _hist = new List<string>();
    int _hp;
    GameObject _lastTarget;

    // ------------------------------------------------------------ жизненный цикл

    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }

    void Start()
    {
        bool noTarget = target == null;
        if (noTarget) target = gameObject;
        _eval.target = target;
        _lastTarget = target;

        if (inputField == null) BuildUI();

        inputField.onEndEdit.AddListener(OnEndEdit);
        if (runButton != null) runButton.onClick.AddListener(Submit);

        Print("Введите команду и нажмите Enter, например:\n  transform.DOMove(new Vector3(3, 0, 0), 1)\n'help' — справка, 'clear' — очистить.");
        if (noTarget)
            Print("<color=#ffd24d>Target не задан. Перетащите объект в поле Target или выполните:\n  target = GameObject.Find(\"Cube\")</color>");

        inputField.ActivateInputField();
    }

    void Update()
    {
        // История команд стрелками вверх/вниз
        if (inputField == null || !inputField.isFocused || _hist.Count == 0) return;
        if (UpPressed())
        {
            _hp = Mathf.Max(0, _hp - 1);
            SetText(_hist[_hp]);
        }
        else if (DownPressed())
        {
            _hp = Mathf.Min(_hist.Count, _hp + 1);
            SetText(_hp < _hist.Count ? _hist[_hp] : "");
        }
    }

    // ------------------------------------------------------------ выполнение

    void OnEndEdit(string text)
    {
        // onEndEdit вызывается и при потере фокуса, поэтому проверяем, что нажат именно Enter
        if (EnterPressed()) Submit();
    }

    void Submit()
    {
        string code = inputField.text.Trim();
        if (code.Length == 0) { inputField.ActivateInputField(); return; }

        _hist.Add(code);
        _hp = _hist.Count;
        Print("<color=#8ec9ff>>> " + code + "</color>");

        // target мог быть изменён в Inspector во время игры
        if (target != null && target != _lastTarget) { _eval.target = target; _lastTarget = target; }

        string lower = code.ToLowerInvariant();
        if (lower == "clear") { _log.Clear(); Redraw(); }
        else if (lower == "help") Print(HelpText);
        else
        {
            try
            {
                string res = _eval.Execute(code);
                Print(string.IsNullOrEmpty(res) ? "<color=#7CFC9A>OK</color>" : "<color=#7CFC9A>" + res + "</color>");
            }
            catch (Exception e)
            {
                if (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
                Print("<color=#ff6666>Ошибка: " + e.Message + "</color>");
            }
            // команда «target = ...» могла сменить цель
            target = _eval.target;
            _lastTarget = target;
        }

        inputField.text = "";
        inputField.ActivateInputField();
    }

    const string HelpText =
        "Поддерживается одна команда в строке или несколько через ';'.\n" +
        "  transform.DOMove(new Vector3(3,0,0), 1)       (DOTween, если он установлен)\n" +
        "  transform.DOMove(...).SetEase(Ease.OutBounce)\n" +
        "  transform.position = new Vector3(1, 2, 3)\n" +
        "  transform.localScale *= 2\n" +
        "  transform.Translate(1, 0, 0)\n" +
        "  target = GameObject.Find(\"Имя\")     — сменить цель\n" +
        "  Debug.Log(\"привет\")\n" +
        "Нет: переменных, if/for, лямбд (OnComplete(() => ...)).";

    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (!showUnityLogs || outputText == null) return;
        string color = type == LogType.Log ? "#bbbbbb" : type == LogType.Warning ? "#ffd24d" : "#ff6666";
        Print("<color=" + color + ">[Unity] " + condition + "</color>");
    }

    // ------------------------------------------------------------ вывод

    void Print(string line)
    {
        _log.Add(line);
        while (_log.Count > maxLines) _log.RemoveAt(0);
        Redraw();
    }

    void Redraw()
    {
        if (outputText != null) outputText.text = string.Join("\n", _log);
    }

    void SetText(string t)
    {
        inputField.text = t;
        inputField.caretPosition = t.Length;
    }

    // ------------------------------------------------------------ ввод (старый и новый Input System)

    static bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    static bool UpPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.upArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    static bool DownPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.downArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    // ------------------------------------------------------------ автосоздание UI

    void BuildUI()
    {
        Font font = LoadFont();

        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        var canvasGO = new GameObject("CodeConsoleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Панель внизу экрана
        var panel = NewUI("Panel", canvasGO.transform);
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        SetRect(panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 20), new Vector2(-20, 360));

        // Вывод
        var outGO = NewUI("Output", panel.transform);
        SetRect(outGO, new Vector2(0, 0.2f), new Vector2(1, 1), new Vector2(12, 8), new Vector2(-12, -8));
        outputText = MakeText(outGO, font, fontSize, Color.white, TextAnchor.LowerLeft, true);
        outputText.supportRichText = true;
        outputText.raycastTarget = false;

        // Поле ввода
        var inGO = NewUI("InputField", panel.transform);
        var bg = inGO.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.2f, 1f);
        SetRect(inGO, new Vector2(0, 0), new Vector2(0.84f, 0.18f), new Vector2(12, 12), new Vector2(-6, 0));

        var txtGO = NewUI("Text", inGO.transform);
        SetRect(txtGO, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
        var txt = MakeText(txtGO, font, fontSize, Color.white, TextAnchor.MiddleLeft, false);
        txt.supportRichText = false;

        var phGO = NewUI("Placeholder", inGO.transform);
        SetRect(phGO, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
        var ph = MakeText(phGO, font, fontSize, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft, false);
        ph.fontStyle = FontStyle.Italic;
        ph.text = "transform.DOMove(new Vector3(3, 0, 0), 1)";

        inputField = inGO.AddComponent<InputField>();
        inputField.targetGraphic = bg;
        inputField.textComponent = txt;
        inputField.placeholder = ph;
        inputField.lineType = InputField.LineType.SingleLine;

        // Кнопка Run
        var btnGO = NewUI("RunButton", panel.transform);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.5f, 0.9f, 1f);
        SetRect(btnGO, new Vector2(0.85f, 0), new Vector2(1, 0.18f), new Vector2(6, 12), new Vector2(-12, 0));
        runButton = btnGO.AddComponent<Button>();
        runButton.targetGraphic = btnImg;

        var btnTxtGO = NewUI("Text", btnGO.transform);
        SetRect(btnTxtGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var btnTxt = MakeText(btnTxtGO, font, fontSize, Color.white, TextAnchor.MiddleCenter, false);
        btnTxt.text = "Run";
        btnTxt.raycastTarget = false;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = oMin; r.offsetMax = oMax;
    }

    static Text MakeText(GameObject go, Font font, int size, Color color, TextAnchor anchor, bool wrap)
    {
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    static Font LoadFont()
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

// =====================================================================================
//  Мини-интерпретатор C#-выражений на рефлексии.
//  Поддерживает: цепочки вызовов/свойств, new Тип(...), числа/строки/true/false/null,
//  + - * /, присваивание (=, +=, -=, *=, /=), статические члены и enum,
//  extension-методы (в том числе DOTween: DOMove, DOScale, SetEase, SetLoops ...).
// =====================================================================================
public class MiniEval
{
    public GameObject target;

    sealed class TypeRef { public Type type; }
    sealed class NsRef { public string name; }
    struct Cand { public MethodBase m; public bool ext; }

    const BindingFlags Inst = BindingFlags.Public | BindingFlags.Instance;
    const BindingFlags Stat = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------ публичный API

    public string Execute(string code)
    {
        var sb = new StringBuilder();
        foreach (var raw in SplitTop(code, ';'))
        {
            string st = raw.Trim();
            if (st.Length == 0) continue;
            string r = RunStatement(st);
            if (!string.IsNullOrEmpty(r))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(r);
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ выполнение оператора

    string RunStatement(string st)
    {
        int eq;
        if (FindAssign(st, out eq))
        {
            char op = '=';
            int lhsEnd = eq;
            if (eq > 0 && "+-*/".IndexOf(st[eq - 1]) >= 0) { op = st[eq - 1]; lhsEnd = eq - 1; }
            Assign(st.Substring(0, lhsEnd).Trim(), op, st.Substring(eq + 1).Trim());
            return "";
        }
        return Format(Eval(st));
    }

    void Assign(string lhs, char op, string rhs)
    {
        if (lhs == "target")
        {
            target = ToGameObject(Eval(rhs));
            return;
        }

        int dot = LastTopDot(lhs);
        if (dot < 0)
            throw new Exception("Слева от '=' должно быть поле или свойство, например: transform.position");

        string objExpr = lhs.Substring(0, dot).Trim();
        string member = lhs.Substring(dot + 1).Trim();

        object owner = EvalRaw(objExpr);
        if (owner is NsRef) throw new Exception("Неизвестное имя: " + ((NsRef)owner).name);

        bool isStatic = owner is TypeRef;
        Type t = isStatic ? ((TypeRef)owner).type : (owner == null ? null : owner.GetType());
        if (t == null) throw new Exception("Объект слева равен null");
        if (!isStatic && t.IsValueType)
            throw new Exception("Нельзя менять поле структуры напрямую. Присвойте целиком, например: transform.position = new Vector3(1, 2, 3)");

        var fl = isStatic ? Stat : Inst;
        object self = isStatic ? null : owner;

        PropertyInfo p = t.GetProperty(member, fl);
        FieldInfo f = p == null ? t.GetField(member, fl) : null;
        if (p == null && f == null) throw new Exception("У " + t.Name + " нет поля или свойства '" + member + "'");

        object value = Eval(rhs);
        Type mt = p != null ? p.PropertyType : f.FieldType;

        if (op != '=')
        {
            object cur = p != null ? p.GetValue(self, null) : f.GetValue(self);
            value = Binary(op, cur, value);
        }

        object conv;
        if (Conv(value, mt, out conv) == 0)
            throw new Exception("Не могу преобразовать значение к типу " + mt.Name);

        if (p != null)
        {
            if (!p.CanWrite) throw new Exception("Свойство '" + member + "' только для чтения");
            p.SetValue(self, conv, null);
        }
        else f.SetValue(self, conv);
    }

    // ------------------------------------------------------------ вычисление выражений

    object EvalRaw(string expr)
    {
        var p = new Parser(this, expr);
        object v = p.ParseExpr();
        p.ExpectEnd();
        return v;
    }

    object Eval(string expr) { return Val(EvalRaw(expr)); }

    object Val(object o)
    {
        if (o is TypeRef) throw new Exception("'" + ((TypeRef)o).type.Name + "' — это тип, а не значение");
        if (o is NsRef) throw new Exception("Неизвестное имя: " + ((NsRef)o).name);
        return o;
    }

    GameObject RequireTarget()
    {
        if (target == null) throw new Exception("Не задан target. Выполните: target = GameObject.Find(\"Имя\")");
        return target;
    }

    object ResolveRoot(string name)
    {
        switch (name)
        {
            case "transform": return RequireTarget().transform;
            case "gameObject":
            case "target": return RequireTarget();
        }
        Type t = FindType(name);
        if (t != null) return new TypeRef { type = t };
        return new NsRef { name = name };
    }

    static GameObject ToGameObject(object v)
    {
        var go = v as GameObject;
        if (go != null) return go;
        var c = v as Component;
        if (c != null) return c.gameObject;
        throw new Exception("Ожидался GameObject, например GameObject.Find(\"Cube\")");
    }

    // ------------------------------------------------------------ доступ к членам

    object GetMember(object cur, string name)
    {
        if (cur is NsRef)
        {
            string full = ((NsRef)cur).name + "." + name;
            Type nt = FindType(full);
            if (nt != null) return new TypeRef { type = nt };
            return new NsRef { name = full };
        }

        bool st = cur is TypeRef;
        Type type = st ? ((TypeRef)cur).type : (cur == null ? null : cur.GetType());
        if (type == null) throw new Exception("Нельзя взять '" + name + "' у null");

        var fl = st ? Stat : Inst;
        object self = st ? null : cur;

        PropertyInfo p = type.GetProperty(name, fl);
        if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(self, null);

        FieldInfo f = type.GetField(name, fl);
        if (f != null) return f.GetValue(self);

        if (st)
        {
            Type nested = type.GetNestedType(name, BindingFlags.Public);
            if (nested != null) return new TypeRef { type = nested };
        }
        throw new Exception("У " + type.Name + " нет члена '" + name + "'");
    }

    object CallMethod(object cur, string name, List<object> args)
    {
        if (cur is NsRef) throw new Exception("Неизвестное имя: " + ((NsRef)cur).name);

        bool st = cur is TypeRef;
        Type type = st ? ((TypeRef)cur).type : (cur == null ? null : cur.GetType());
        if (type == null) throw new Exception("Нельзя вызвать '" + name + "' у null");

        var cands = new List<Cand>();
        foreach (var m in type.GetMethods(st ? Stat : Inst))
        {
            if (m.Name != name || m.IsGenericMethodDefinition) continue;
            cands.Add(new Cand { m = m });
        }

        if (!st)
        {
            foreach (var m in GetExtensions(name))
            {
                MethodInfo mi = m;
                var ps0 = mi.GetParameters();
                if (ps0.Length == 0) continue;
                if (mi.IsGenericMethodDefinition)
                {
                    // Поддерживаем вид  T Method<T>(this T t, ...)  — как SetEase/SetLoops в DOTween
                    if (mi.GetGenericArguments().Length != 1 || !ps0[0].ParameterType.IsGenericParameter) continue;
                    try { mi = mi.MakeGenericMethod(type); } catch { continue; }
                    ps0 = mi.GetParameters();
                }
                if (!ps0[0].ParameterType.IsAssignableFrom(type)) continue;
                cands.Add(new Cand { m = mi, ext = true });
            }
        }

        if (cands.Count == 0) throw new Exception("У " + type.Name + " нет метода '" + name + "'");
        return Invoke(cands, st ? null : cur, args, type.Name + "." + name);
    }

    object Construct(Type t, List<object> args)
    {
        var cands = new List<Cand>();
        foreach (var c in t.GetConstructors(Inst)) cands.Add(new Cand { m = c });

        if (args.Count == 0 && t.IsValueType) return Activator.CreateInstance(t);
        if (cands.Count == 0) throw new Exception("У типа " + t.Name + " нет публичного конструктора");
        return Invoke(cands, null, args, "new " + t.Name);
    }

    // ------------------------------------------------------------ выбор перегрузки и вызов

    static object Invoke(List<Cand> cands, object self, List<object> args, string desc)
    {
        MethodBase best = null;
        object[] bestArgs = null;
        int bestScore = -1, bestParams = int.MaxValue;

        foreach (var c in cands)
        {
            var ps = c.m.GetParameters();
            int off = c.ext ? 1 : 0;
            int n = ps.Length - off;
            if (args.Count > n) continue;

            var call = new object[ps.Length];
            if (c.ext) call[0] = self;

            int score = 0;
            bool ok = true;
            for (int k = 0; k < n; k++)
            {
                var p = ps[k + off];
                if (p.ParameterType.IsByRef || p.IsOut) { ok = false; break; }

                if (k < args.Count)
                {
                    object cv;
                    int sc = Conv(args[k], p.ParameterType, out cv);
                    if (sc == 0) { ok = false; break; }
                    call[k + off] = cv;
                    score += sc;
                }
                else if (p.IsOptional)
                {
                    call[k + off] = p.HasDefaultValue ? p.DefaultValue : Type.Missing;
                }
                else { ok = false; break; }
            }
            if (!ok) continue;

            if (score > bestScore || (score == bestScore && ps.Length < bestParams))
            {
                best = c.m; bestArgs = call; bestScore = score; bestParams = ps.Length;
            }
        }

        if (best == null)
        {
            var types = new List<string>();
            foreach (var a in args) types.Add(a == null ? "null" : a.GetType().Name);
            throw new Exception("Нет подходящей перегрузки " + desc + "(" + string.Join(", ", types) + ")");
        }

        var ci = best as ConstructorInfo;
        if (ci != null) return ci.Invoke(bestArgs);
        return best.Invoke(best.IsStatic ? null : self, bestArgs);
    }

    // Оценка преобразования: 0 — нельзя, больше — точнее
    static int Conv(object v, Type t, out object res)
    {
        res = null;
        Type ut = Nullable.GetUnderlyingType(t);
        if (ut != null)
        {
            if (v == null) return 2;
            t = ut;
        }
        if (v == null) return t.IsValueType ? 0 : 2;

        Type vt = v.GetType();
        if (vt == t) { res = v; return 4; }
        if (t.IsAssignableFrom(vt)) { res = v; return 3; }

        if (t.IsEnum)
        {
            string sv = v as string;
            if (sv != null)
            {
                try { res = Enum.Parse(t, sv, true); return 2; } catch { return 0; }
            }
            if (IsNumType(vt) && !IsFloat(vt)) { res = Enum.ToObject(t, Convert.ToInt64(v, Inv)); return 1; }
            return 0;
        }

        if (IsNumType(vt) && IsNumType(t))
        {
            if (IsIntegral(t) && IsFloat(vt)) return 0;
            try { res = Convert.ChangeType(v, t, Inv); return 1; } catch { return 0; }
        }

        if (t == typeof(GameObject))
        {
            var c = v as Component;
            if (c != null) { res = c.gameObject; return 1; }
            return 0;
        }

        if (typeof(Component).IsAssignableFrom(t))
        {
            GameObject go = v as GameObject;
            if (go == null)
            {
                var c2 = v as Component;
                if (c2 != null) go = c2.gameObject;
            }
            if (go != null)
            {
                var comp = go.GetComponent(t);
                if (comp != null) { res = comp; return 1; }
            }
            return 0;
        }
        return 0;
    }

    // ------------------------------------------------------------ арифметика

    static object Binary(char op, object a, object b)
    {
        if (a is string || b is string)
        {
            if (op == '+') return string.Concat(a, b);
            throw new Exception("Для строк поддерживается только '+'");
        }
        if (a == null || b == null) throw new Exception("null в арифметике");

        if (IsNumType(a.GetType()) && IsNumType(b.GetType()))
        {
            if (a is int && b is int)
            {
                int x = (int)a, y = (int)b;
                switch (op)
                {
                    case '+': return x + y;
                    case '-': return x - y;
                    case '*': return x * y;
                    default:
                        if (y == 0) throw new DivideByZeroException();
                        return x / y;
                }
            }
            if (a is double || b is double)
            {
                double x = Convert.ToDouble(a, Inv), y = Convert.ToDouble(b, Inv);
                switch (op)
                {
                    case '+': return x + y;
                    case '-': return x - y;
                    case '*': return x * y;
                    default: return x / y;
                }
            }
            else
            {
                float x = Convert.ToSingle(a, Inv), y = Convert.ToSingle(b, Inv);
                switch (op)
                {
                    case '+': return x + y;
                    case '-': return x - y;
                    case '*': return x * y;
                    default: return x / y;
                }
            }
        }

        string opName = op == '+' ? "op_Addition" : op == '-' ? "op_Subtraction" : op == '*' ? "op_Multiply" : "op_Division";
        var cands = new List<Cand>();
        AddOperators(cands, a.GetType(), opName);
        if (b.GetType() != a.GetType()) AddOperators(cands, b.GetType(), opName);
        if (cands.Count == 0)
            throw new Exception("Для " + a.GetType().Name + " и " + b.GetType().Name + " нет оператора '" + op + "'");
        return Invoke(cands, null, new List<object> { a, b }, a.GetType().Name + " " + op + " " + b.GetType().Name);
    }

    static object Negate(object v)
    {
        if (v is int) return -(int)v;
        if (v is float) return -(float)v;
        if (v is double) return -(double)v;
        if (v != null && IsNumType(v.GetType())) return -Convert.ToDouble(v, Inv);
        if (v == null) throw new Exception("Нельзя взять минус от null");

        var cands = new List<Cand>();
        AddOperators(cands, v.GetType(), "op_UnaryNegation");
        if (cands.Count == 0) throw new Exception("Для " + v.GetType().Name + " нет унарного минуса");
        return Invoke(cands, null, new List<object> { v }, "-" + v.GetType().Name);
    }

    static void AddOperators(List<Cand> cands, Type t, string name)
    {
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (m.Name == name) cands.Add(new Cand { m = m });
    }

    static bool IsNumType(Type t)
    {
        if (t.IsEnum) return false;
        switch (Type.GetTypeCode(t))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return true;
        }
        return false;
    }

    static bool IsFloat(Type t)
    {
        var c = Type.GetTypeCode(t);
        return c == TypeCode.Single || c == TypeCode.Double || c == TypeCode.Decimal;
    }

    static bool IsIntegral(Type t) { return IsNumType(t) && !IsFloat(t); }

    // ------------------------------------------------------------ вывод результата

    static string Format(object v)
    {
        if (v == null) return "";
        if (v is string) return "=> \"" + v + "\"";
        var uo = v as UnityEngine.Object;
        if (uo != null) return "=> " + uo.name + " (" + v.GetType().Name + ")";
        if (v.GetType().IsValueType)
        {
            var fmt = v as IFormattable;
            return "=> " + (fmt != null ? fmt.ToString(null, Inv) : v.ToString());
        }
        return "";   // например, Tween — просто «OK»
    }

    // ------------------------------------------------------------ поиск типов и extension-методов

    static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
    static readonly string[] Prefixes = { "UnityEngine.", "DG.Tweening.", "", "UnityEngine.UI.", "TMPro.", "System." };

    static Type FindType(string name)
    {
        Type found;
        if (TypeCache.TryGetValue(name, out found)) return found;

        var asms = AppDomain.CurrentDomain.GetAssemblies();
        string[] prefixes = name.IndexOf('.') >= 0 ? new[] { "" } : Prefixes;
        found = null;
        foreach (var prefix in prefixes)
        {
            foreach (var asm in asms)
            {
                Type t = null;
                try { t = asm.GetType(prefix + name, false); } catch { }
                if (t != null && (t.IsPublic || t.IsNestedPublic)) { found = t; break; }
            }
            if (found != null) break;
        }
        TypeCache[name] = found;
        return found;
    }

    static Dictionary<string, List<MethodInfo>> _ext;
    static readonly List<MethodInfo> NoMethods = new List<MethodInfo>();

    static List<MethodInfo> GetExtensions(string name)
    {
        if (_ext == null) BuildExtensions();
        List<MethodInfo> l;
        return _ext.TryGetValue(name, out l) ? l : NoMethods;
    }

    static void BuildExtensions()
    {
        _ext = new Dictionary<string, List<MethodInfo>>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (SkipAssembly(asm.GetName().Name)) continue;

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            catch { continue; }

            foreach (var t in types)
            {
                if (t == null || !t.IsSealed || !t.IsAbstract || t.IsNested || !t.IsPublic) continue;
                if (!t.IsDefined(typeof(ExtensionAttribute), false)) continue;

                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!m.IsDefined(typeof(ExtensionAttribute), false)) continue;
                    List<MethodInfo> l;
                    if (!_ext.TryGetValue(m.Name, out l)) { l = new List<MethodInfo>(); _ext[m.Name] = l; }
                    l.Add(m);
                }
            }
        }
    }

    static bool SkipAssembly(string n)
    {
        return n.StartsWith("System") || n == "mscorlib" || n.StartsWith("netstandard")
            || n.StartsWith("Mono.") || n == "Mono" || n.StartsWith("Microsoft")
            || n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor") || n.StartsWith("Unity.")
            || n.StartsWith("nunit") || n.StartsWith("Newtonsoft");
    }

    // ------------------------------------------------------------ разбор строк

    static List<string> SplitTop(string s, char sep)
    {
        var res = new List<string>();
        int depth = 0, start = 0;
        bool str = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
            if (c == '"') str = true;
            else if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (c == sep && depth == 0) { res.Add(s.Substring(start, i - start)); start = i + 1; }
        }
        res.Add(s.Substring(start));
        return res;
    }

    static bool FindAssign(string s, out int pos)
    {
        pos = -1;
        int depth = 0;
        bool str = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
            if (c == '"') { str = true; continue; }
            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (c == '=' && depth == 0)
            {
                char next = i + 1 < s.Length ? s[i + 1] : '\0';
                char prev = i > 0 ? s[i - 1] : '\0';
                if (next == '=') { i++; continue; }
                if (prev == '!' || prev == '<' || prev == '>' || prev == '=') continue;
                pos = i;
                return true;
            }
        }
        return false;
    }

    static int LastTopDot(string s)
    {
        int depth = 0, last = -1;
        bool str = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
            if (c == '"') str = true;
            else if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (c == '.' && depth == 0) last = i;
        }
        return last;
    }

    // ------------------------------------------------------------ рекурсивный парсер

    sealed class Parser
    {
        readonly MiniEval ev;
        readonly string s;
        int i;

        public Parser(MiniEval ev, string s) { this.ev = ev; this.s = s; }

        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        bool Peek(char c) { Ws(); return i < s.Length && s[i] == c; }

        bool Eat(char c)
        {
            if (Peek(c)) { i++; return true; }
            return false;
        }

        void Expect(char c)
        {
            if (!Eat(c)) throw new Exception("Ожидалось '" + c + "' (позиция " + i + ")");
        }

        public void ExpectEnd()
        {
            Ws();
            if (i < s.Length) throw new Exception("Неожиданный символ '" + s[i] + "' (позиция " + i + ")");
        }

        public object ParseExpr() { return ParseAdd(); }

        object ParseAdd()
        {
            object l = ParseMul();
            while (true)
            {
                Ws();
                if (i < s.Length && (s[i] == '+' || s[i] == '-'))
                {
                    char op = s[i++];
                    object r = ParseMul();
                    l = Binary(op, ev.Val(l), ev.Val(r));
                }
                else break;
            }
            return l;
        }

        object ParseMul()
        {
            object l = ParseUnary();
            while (true)
            {
                Ws();
                if (i < s.Length && (s[i] == '*' || s[i] == '/'))
                {
                    char op = s[i++];
                    object r = ParseUnary();
                    l = Binary(op, ev.Val(l), ev.Val(r));
                }
                else break;
            }
            return l;
        }

        object ParseUnary()
        {
            if (Eat('-')) return Negate(ev.Val(ParseUnary()));
            if (Eat('+')) return ParseUnary();
            return ParsePostfix();
        }

        object ParsePostfix()
        {
            object cur = ParsePrimary();
            while (true)
            {
                Ws();
                if (i < s.Length && s[i] == '.')
                {
                    i++;
                    Ws();
                    string name = ReadIdent();
                    if (Peek('(')) cur = ev.CallMethod(cur, name, ParseArgs());
                    else cur = ev.GetMember(cur, name);
                }
                else break;
            }
            return cur;
        }

        object ParsePrimary()
        {
            Ws();
            if (i >= s.Length) throw new Exception("Неожиданный конец выражения");
            char c = s[i];

            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))) return ReadNumber();
            if (c == '"') return ReadString();
            if (c == '(')
            {
                i++;
                object v = ParseExpr();
                Expect(')');
                return v;
            }
            if (char.IsLetter(c) || c == '_')
            {
                string name = ReadIdent();
                switch (name)
                {
                    case "true": return true;
                    case "false": return false;
                    case "null": return null;
                    case "new": return ParseNew();
                }
                if (Peek('('))
                    throw new Exception("Вызов '" + name + "(...)' без объекта не поддерживается. Пишите, например: transform.DOMove(...)");
                return ev.ResolveRoot(name);
            }
            throw new Exception("Неожиданный символ '" + c + "' (позиция " + i + ")");
        }

        object ParseNew()
        {
            Ws();
            string tn = ReadIdent();
            while (i < s.Length && s[i] == '.') { i++; tn += "." + ReadIdent(); }
            Type t = FindType(tn);
            if (t == null) throw new Exception("Неизвестный тип: " + tn);
            return ev.Construct(t, ParseArgs());
        }

        List<object> ParseArgs()
        {
            Expect('(');
            var list = new List<object>();
            if (Eat(')')) return list;
            while (true)
            {
                list.Add(ev.Val(ParseExpr()));
                if (Eat(',')) continue;
                Expect(')');
                break;
            }
            return list;
        }

        string ReadIdent()
        {
            int st = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            if (st == i) throw new Exception("Ожидалось имя (позиция " + i + ")");
            return s.Substring(st, i - st);
        }

        object ReadNumber()
        {
            int st = i;
            bool dot = false;
            while (i < s.Length && (char.IsDigit(s[i]) ||
                   (s[i] == '.' && !dot && i + 1 < s.Length && char.IsDigit(s[i + 1]))))
            {
                if (s[i] == '.') dot = true;
                i++;
            }
            string num = s.Substring(st, i - st);
            char suf = i < s.Length ? char.ToLowerInvariant(s[i]) : '\0';
            if (suf == 'f') { i++; return float.Parse(num, Inv); }
            if (suf == 'd') { i++; return double.Parse(num, Inv); }
            if (dot) return double.Parse(num, Inv);
            int iv;
            if (int.TryParse(num, NumberStyles.Integer, Inv, out iv)) return iv;
            return double.Parse(num, Inv);
        }

        object ReadString()
        {
            i++;   // открывающая кавычка
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            if (i >= s.Length) throw new Exception("Не закрыта строка");
            i++;   // закрывающая кавычка
            return sb.ToString();
        }
    }
}