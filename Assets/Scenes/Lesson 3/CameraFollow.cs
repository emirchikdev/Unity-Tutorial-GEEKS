using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("За кем летим")]
    public Transform target;                         // сюда перетащить поезд

    [Header("Где находится камера")]
    public Vector3 offset = new Vector3(0f, 6f, -10f); // смещение относительно поезда
    public bool rotateWithTarget = false;            // true - камера держится позади поезда и поворачивается вместе с ним

    [Header("Плавность")]
    public float moveSmooth = 5f;                    // чем больше, тем быстрее камера догоняет
    public float lookSmooth = 8f;                    // чем больше, тем быстрее камера поворачивается

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Куда камера хочет попасть
        Vector3 wantedPosition;
        if (rotateWithTarget)
        {
            wantedPosition = target.position + target.rotation * offset;
        }
        else
        {
            wantedPosition = target.position + offset;
        }

        // Плавно летим к этой точке
        transform.position = Vector3.Lerp(transform.position, wantedPosition, moveSmooth * Time.deltaTime);

        // Плавно смотрим на поезд
        Quaternion wantedRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, lookSmooth * Time.deltaTime);
    }
}