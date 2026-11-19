using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ForLoopDemo : MonoBehaviour
{
    [Header("Поля ввода (на Canvas)")]
    public TMP_InputField startInput;      // с чего начинаем (i = ...)
    public TMP_InputField endInput;        // до чего считаем
    public TMP_InputField stepInput;       // на сколько растёт i каждый раз
    public TMP_Dropdown operatorDropdown;  // < или <= (необязательно)

    [Header("Кнопки (на Canvas), необязательно")]
    public Button runButton;               // "Запустить"
    public Button resetButton;             // "Сначала"

    [Header("Сцена")]
    public GameObject itemPrefab;          // предмет, который создаёт цикл (внутри может быть TMP_Text для номера)
    public Transform spawnOrigin;          // откуда начинаем выкладывать предметы
    public float spacing = 1.5f;           // расстояние между предметами
    public float stepDelay = 0.9f;         // пауза между шагами объяснения
    public int maxIterations = 15;         // больше повторов не разрешаем

    [Header("Что видит ребёнок (все TMP_Text)")]
    public TMP_Text forLine;               // for (int i = 0; i < 5; i++)
    public TMP_Text bodyLine;              // тело цикла
    public TMP_Text counterText;           // i = 3
    public TMP_Text statusText;            // пошаговое объяснение
    public TMP_Text messageText;           // ошибки и итог (должен переносить строки)
    public string bodyCode = "{  СоздатьПредмет(i);  }";

    [Header("Цвета")]
    public Color normalColor = Color.white;
    public Color activeColor = new Color(0.3f, 1f, 0.3f);
    public Color dimColor = new Color(1f, 1f, 1f, 0.3f);
    public Color okColor = new Color(0.3f, 1f, 0.3f);
    public Color errorColor = new Color(1f, 0.35f, 0.35f);
    public Color noteColor = new Color(1f, 0.85f, 0.3f);

    private readonly List<GameObject> items = new List<GameObject>();
    private Coroutine routine;
    private bool running = false;
    private Vector3 counterBaseScale;

    void Start()
    {
        if (itemPrefab == null || spawnOrigin == null)
        {
            Debug.LogError("ForLoopDemo: не назначен Item Prefab или Spawn Origin.");
        }

        counterBaseScale = counterText.transform.localScale;

        // Значения по умолчанию, чтобы сразу было что запускать
        if (startInput.text == "") startInput.text = "0";
        if (endInput.text == "") endInput.text = "5";
        if (stepInput.text == "") stepInput.text = "1";

        if (operatorDropdown != null)
        {
            operatorDropdown.ClearOptions();
            operatorDropdown.AddOptions(new List<string> { "<", "<=" });
            operatorDropdown.onValueChanged.AddListener(delegate { UpdateCodeLine(); });
        }

        // Строка for обновляется прямо во время набора цифр
        startInput.onValueChanged.AddListener(delegate { UpdateCodeLine(); });
        endInput.onValueChanged.AddListener(delegate { UpdateCodeLine(); });
        stepInput.onValueChanged.AddListener(delegate { UpdateCodeLine(); });

        // Если кнопки перетащены в поля, подключаем их сами
        if (runButton != null) runButton.onClick.AddListener(Run);
        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);

        ResetAll();
    }

    // Кнопка "Запустить"
    public void Run()
    {
        StopRun();
        ClearItems();
        statusText.text = "";
        messageText.text = "";

        int start, end, step;
        if (!ReadField(startInput, "Начало", out start)) return;
        if (!ReadField(endInput, "Конец", out end)) return;
        if (!ReadField(stepInput, "Шаг", out step)) return;

        if (step <= 0)
        {
            Reject("Шаг должен быть больше нуля.",
                "Если шаг 0, число i никогда не меняется, и цикл не закончится никогда. "
                + "Это называется бесконечный цикл. Отрицательный шаг тоже не приведёт i к концу.");
            return;
        }

        bool inclusive = GetOperator() == "<=";

        // Считаем заранее, сколько будет повторов
        long count = 0;
        if (inclusive)
        {
            if (start <= end) count = ((long)end - start) / step + 1;
        }
        else
        {
            if (start < end) count = ((long)end - start - 1) / step + 1;
        }

        if (count > maxIterations)
        {
            Reject("Слишком много повторов: " + count + ". Максимум " + maxIterations + ".",
                "Уменьши конец или увеличь шаг, чтобы предметы поместились на сцене.");
            return;
        }

        routine = StartCoroutine(RunLoop(start, end, step, inclusive));
    }

    // Здесь работает настоящий цикл for, просто с паузами, чтобы всё было видно
    private IEnumerator RunLoop(int start, int end, int step, bool inclusive)
    {
        running = true;
        WaitForSeconds wait = new WaitForSeconds(stepDelay);
        string op = inclusive ? "<=" : "<";

        int i;
        int count = 0;
        int lastI = start;
        string seen = "";

        // 1. Начало: один раз создаём i
        Highlight(forLine);
        SetCounter(start);
        Say("Старт. Создаём переменную i и кладём в неё " + start + ". Это происходит один раз.");
        yield return wait;

        for (i = start; Check(i, end, inclusive); i += step)
        {
            // 2. Проверка условия
            Highlight(forLine);
            SetCounter(i);
            Say("Проверка: " + i + " " + op + " " + end + "? Да. Заходим в цикл.");
            yield return wait;

            // 3. Тело цикла
            Highlight(bodyLine);
            SpawnItem(i, count);
            count++;
            lastI = i;
            seen += (seen.Length == 0 ? "" : ", ") + i;
            Say("Тело цикла: создаём предмет с номером " + i + ".");
            yield return wait;

            // 4. Шаг
            Highlight(forLine);
            if (step == 1)
            {
                Say("Шаг: i++ (это то же самое, что i = i + 1). Теперь i равно " + (i + step) + ".");
            }
            else
            {
                Say("Шаг: i += " + step + " (это i = i + " + step + "). Теперь i равно " + (i + step) + ".");
            }
            SetCounter(i + step);
            yield return wait;
        }

        // 5. Условие стало ложным: цикл закончился
        Highlight(forLine);
        SetCounter(i);
        Say("Проверка: " + i + " " + op + " " + end + "? Нет. Цикл закончился.");
        yield return wait;

        Highlight(null);
        ShowFinalMessage(count, seen, lastI, start, end, step, inclusive);
        running = false;
    }

    private void ShowFinalMessage(int count, string seen, int lastI, int start, int end, int step, bool inclusive)
    {
        string op = inclusive ? "<=" : "<";

        if (count == 0)
        {
            ShowMessage("Тело цикла не выполнилось ни разу.", errorColor,
                "Условие " + start + " " + op + " " + end + " ложно с самого начала, поэтому цикл даже не начался.");
            return;
        }

        string note;
        if (inclusive)
        {
            note = "Оператор <= включает конец: число " + end + " ещё проходит проверку.";
        }
        else
        {
            note = "Оператор < не включает конец: число " + end + " уже не проходит проверку, поэтому последнее i равно "
                 + lastI + ". Хочешь включить конец, выбери <=.";
        }

        if (step > 1)
        {
            note += " Шаг " + step + ": i растёт сразу на " + step + ", поэтому номера идут с пропусками.";
        }

        ShowMessage("Повторов: " + count + ". Значения i: " + seen + ".", okColor, note);
    }

    // Кнопка "Сначала"
    public void ResetAll()
    {
        StopRun();
        ClearItems();
        statusText.text = "";
        messageText.text = "";
        counterText.text = "i = ?";
        bodyLine.text = bodyCode;
        UpdateCodeLine();
    }

    // ---------- вспомогательное ----------

    private bool Check(int i, int end, bool inclusive)
    {
        return inclusive ? i <= end : i < end;
    }

    private string GetOperator()
    {
        if (operatorDropdown != null && operatorDropdown.value == 1)
        {
            return "<=";
        }
        return "<";
    }

    // Строка for собирается из того, что набрали в полях
    private void UpdateCodeLine()
    {
        if (running)
        {
            return;
        }

        string start = startInput.text.Trim();
        string end = endInput.text.Trim();
        string stepRaw = stepInput.text.Trim();

        string stepPart;
        if (stepRaw == "1")
        {
            stepPart = "i++";
        }
        else
        {
            stepPart = "i += " + stepRaw;
        }

        forLine.text = "for (int i = " + start + "; i " + GetOperator() + " " + end + "; " + stepPart + ")";
        bodyLine.text = bodyCode;
    }

    private bool ReadField(TMP_InputField input, string label, out int value)
    {
        value = 0;
        string raw = input.text.Trim();

        if (raw.Length == 0)
        {
            Reject("Поле " + Quote(label) + " пустое.", "Впиши целое число.");
            return false;
        }
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            Reject("Поле " + Quote(label) + " должно быть целым числом, а ты ввёл " + Quote(raw) + ".",
                "Как и в коробке int: только целые числа, без точки и букв.");
            return false;
        }
        if (value < -1000 || value > 1000)
        {
            Reject("В поле " + Quote(label) + " возьми число поменьше: от -1000 до 1000.", null);
            return false;
        }
        return true;
    }

    private void SpawnItem(int number, int index)
    {
        if (itemPrefab == null || spawnOrigin == null)
        {
            return;
        }

        Vector3 position = spawnOrigin.position + Vector3.right * (index * spacing);
        GameObject item = Instantiate(itemPrefab, position, Quaternion.identity);
        items.Add(item);

        TMP_Text label = item.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = number.ToString();
        }

        Vector3 finalScale = item.transform.localScale;
        item.transform.localScale = Vector3.zero;
        item.transform.DOScale(finalScale, 0.3f).SetEase(Ease.OutBack);
    }

    private void ClearItems()
    {
        foreach (GameObject item in items)
        {
            if (item != null)
            {
                item.transform.DOKill();
                Destroy(item);
            }
        }
        items.Clear();
    }

    private void StopRun()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        running = false;
        Highlight(null);
    }

    // Подсвечиваем строку, которая сейчас "работает", остальные тускнеют
    private void Highlight(TMP_Text active)
    {
        SetLine(forLine, active == null || active == forLine, active == forLine);
        SetLine(bodyLine, active == null || active == bodyLine, active == bodyLine);
    }

    private void SetLine(TMP_Text line, bool visible, bool isActive)
    {
        if (isActive)
        {
            line.color = activeColor;
            line.fontStyle = FontStyles.Bold;
        }
        else if (visible)
        {
            line.color = normalColor;
            line.fontStyle = FontStyles.Normal;
        }
        else
        {
            line.color = dimColor;
            line.fontStyle = FontStyles.Normal;
        }
    }

    private void SetCounter(int value)
    {
        counterText.text = "i = " + value;
        counterText.transform.DOKill(true);
        counterText.transform.localScale = counterBaseScale;
        counterText.transform.DOPunchScale(counterBaseScale * 0.25f, 0.3f);
    }

    private void Say(string text)
    {
        statusText.text = text;
    }

    private void Reject(string text, string note)
    {
        ShowMessage(text, errorColor, note);
    }

    // Основное сообщение своим цветом, пояснение под ним жёлтым
    private void ShowMessage(string text, Color color, string note)
    {
        messageText.color = color;

        if (string.IsNullOrEmpty(note))
        {
            messageText.text = text;
        }
        else
        {
            messageText.text = text + "\n<color=#" + ColorUtility.ToHtmlStringRGB(noteColor) + ">" + note + "</color>";
        }
    }

    private string Quote(string text)
    {
        return "\"" + text + "\"";
    }
}