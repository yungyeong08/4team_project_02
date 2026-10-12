using UnityEngine;

/// <summary>Pixel-friendly camera follow for the prototype map.</summary>
public sealed class CameraFollow2D : MonoBehaviour
{
    public Transform Target { get; set; }
    [SerializeField] private float followSpeed = 7f;
    private Vector2 worldSize = new(44f, 30f);

    public void SetWorldBounds(Vector2 size)
    {
        worldSize = size;
    }

    private void LateUpdate()
    {
        if (Target == null) return;
        Camera cameraComponent = GetComponent<Camera>();
        float halfHeight = cameraComponent != null ? cameraComponent.orthographicSize : 5.2f;
        float halfWidth = cameraComponent != null ? halfHeight * cameraComponent.aspect : 9.25f;
        float x = Mathf.Clamp(Target.position.x, -worldSize.x * .5f + halfWidth, worldSize.x * .5f - halfWidth);
        float y = Mathf.Clamp(Target.position.y, -worldSize.y * .5f + halfHeight, worldSize.y * .5f - halfHeight);
        Vector3 desired = new(x, y, -10f);
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
