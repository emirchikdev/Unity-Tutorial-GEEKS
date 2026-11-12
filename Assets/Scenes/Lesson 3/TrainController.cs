using UnityEngine;
using DG.Tweening;

public class TrainController : MonoBehaviour
{
    [Header("Что вводит ребёнок")]
    public int number = 1;              // 1 - левый путь, 2 - правый, любое другое - прямо

    [Header("Точки пути (перетащить из сцены)")]
    public Transform[] leftPath;
    public Transform[] rightPath;
    public Transform[] straightPath;

    [Header("Настройки поезда")]
    public float speed = 8f;            // скорость, метров в секунду
    public bool rigidbodyOn = true;     // галочка "Rigidbody включён"

    [Header("Подсветка условия (необязательно)")]
    public IfVisualizer visualizer;

    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Sequence currentMove;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Поезд едет по заданному пути и толкает всё на своём пути
        rb.isKinematic = true;
    }

    // Эту функцию вызывает кнопка "Запустить"
    public void Run()
    {
        Stop();

        // Физика поезда: включена - сбивает предметы, выключена - проезжает сквозь них
        rb.detectCollisions = rigidbodyOn;

        // if / else: выбираем путь по числу
        Transform[] path;
        if (number == 1)
        {
            path = leftPath;
            if (visualizer != null) visualizer.Show(0);
        }
        else if (number == 2)
        {
            path = rightPath;
            if (visualizer != null) visualizer.Show(1);
        }
        else
        {
            path = straightPath;
            if (visualizer != null) visualizer.Show(2);
        }

        MoveAlong(path);
    }

    // Едем по точкам одну за другой
    private void MoveAlong(Transform[] path)
    {
        currentMove = DOTween.Sequence();
        Vector3 from = transform.position;

        foreach (Transform point in path)
        {
            Vector3 target = point.position;
            float time = Vector3.Distance(from, target) / speed;

            // Поворачиваем поезд носом к следующей точке
            currentMove.AppendCallback(() =>
            {
                Vector3 look = new Vector3(target.x, transform.position.y, target.z);
                transform.LookAt(look);
            });

            currentMove.Append(rb.DOMove(target, time).SetEase(Ease.Linear));
            from = target;
        }
    }

    // Кнопка "Сначала": вернуть поезд на старт
    public void ResetTrain()
    {
        Stop();
        if (visualizer != null) visualizer.Clear();
        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
    }

    private void Stop()
    {
        if (currentMove != null)
        {
            currentMove.Kill();
        }
    }
}