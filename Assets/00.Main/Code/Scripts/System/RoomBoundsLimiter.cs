using UnityEngine;

public class RoomBoundsLimiter : MonoBehaviour
{
    public Vector2 min = new(-1.7f, -1.7f);
    public Vector2 max = new(1.7f, 1.7f);
    public Transform trackedTransform;

    private void OnEnable()
    {
        // Changed: XR tracking pose가 render 직전 갱신된 뒤에도 한 번 더 경계 보정.
        // Why: HMD camera transform은 LateUpdate 이후 BeforeRender 단계에서 다시 벽 밖 위치로 갱신될 수 있음.
        Application.onBeforeRender += ClampToBounds;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= ClampToBounds;
    }

    private void Awake()
    {
        // Changed: 씬에 trackedTransform이 serialize되어 있지 않아도 XR Origin 하위 Camera를 자동 참조.
        // Why: Build Main Scene 재생성 전 기존 씬에서는 RoomBoundsLimiter만 있고 camera 기준점이 비어 있어 root clamp만 적용됨.
        if (trackedTransform != null) return;

        Camera trackedCamera = GetComponentInChildren<Camera>(true);
        if (trackedCamera == null) trackedCamera = Camera.main;
        if (trackedCamera != null) trackedTransform = trackedCamera.transform;
    }

    private void LateUpdate()
    {
        // Changed: XR Origin root뿐 아니라 tracked camera 위치 기준으로 방 내부 XZ 범위를 clamp.
        // Why: 퀘스트에서 사용자가 실제로 움직이면 camera child만 벽 밖으로 나갈 수 있으므로 시점 기준 보정이 필요함.
        ClampToBounds();
    }

    private void ClampToBounds()
    {
        // Changed: clamp 계산을 LateUpdate/BeforeRender에서 공유.
        // Why: XR tracking update timing이 달라도 같은 camera 기준 보정을 적용하기 위함.
        Transform reference = trackedTransform != null ? trackedTransform : transform;
        Vector3 refPos = reference.position;
        Vector3 correction = new(
            Mathf.Clamp(refPos.x, min.x, max.x) - refPos.x,
            0f,
            Mathf.Clamp(refPos.z, min.y, max.y) - refPos.z
        );

        if (correction.sqrMagnitude > 0f)
            transform.position += correction;
    }
}
