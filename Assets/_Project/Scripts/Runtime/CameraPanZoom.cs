using UnityEngine;
using UnityEngine.EventSystems;

public class CameraPanZoom : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;

    [Header("Pan")]
    public float dragSpeed = 1.0f;
    public float minX = -999f;
    public float maxX = 999f;
    public float minZ = -999f;
    public float maxZ = 999f;

    [Header("Zoom (Orthographic Size)")]
    public float minOrthoSize = 15f;
    public float maxOrthoSize = 25f;
    public float pinchZoomSpeed = 0.02f;

    [Header("UI rule")]
    [Tooltip("If drag/touch started over UI, camera is blocked until release.")]
    public bool blockCameraWhenStartedOverUI = true;

    bool _blockMouseUntilUp;
    int _blockTouchFingerId = -1;

    Vector2 _lastMousePos;
    bool _mousePanning;

    Vector2 _lastTouchPos;
    bool _touchPanning;

    void Reset() => cam = Camera.main;

    void Update()
    {
        if (cam == null) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#endif
        HandleTouch();
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    void HandleMouse()
    {
        // zoom колесиком (для теста в редакторе)
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float size = cam.orthographicSize - scroll * 1.0f;
            cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
        }

        if (Input.GetMouseButtonDown(0))
        {
            _mousePanning = false;

            if (blockCameraWhenStartedOverUI && IsMouseOverUI())
            {
                _blockMouseUntilUp = true;
                return;
            }

            _blockMouseUntilUp = false;
            _lastMousePos = Input.mousePosition;
            _mousePanning = true;
        }

        if (Input.GetMouseButton(0))
        {
            if (_blockMouseUntilUp) return;
            if (!_mousePanning) return;

            Vector2 cur = Input.mousePosition;
            Vector2 delta = cur - _lastMousePos;
            _lastMousePos = cur;

            PanByScreenDelta(delta);
        }

        if (Input.GetMouseButtonUp(0))
        {
            _blockMouseUntilUp = false;
            _mousePanning = false;
        }
    }

    bool IsMouseOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
#endif

    void HandleTouch()
    {
        int tc = Input.touchCount;
        if (tc == 0)
        {
            _touchPanning = false;
            _blockTouchFingerId = -1;
            return;
        }

        // Если палец "заблокирован UI", игнорируем камеру до отпускания этого пальца
        if (_blockTouchFingerId != -1)
        {
            for (int i = 0; i < tc; i++)
            {
                var touchCheck = Input.GetTouch(i);
                if (touchCheck.fingerId == _blockTouchFingerId &&
                    (touchCheck.phase == TouchPhase.Ended || touchCheck.phase == TouchPhase.Canceled))
                {
                    _blockTouchFingerId = -1;
                    _touchPanning = false;
                    break;
                }
            }

            if (_blockTouchFingerId != -1) return;
        }

        // Pinch zoom (2 пальца)
        if (tc >= 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            // если какой-то из двух пальцев начался на UI — блокируем до отпускания
            if (blockCameraWhenStartedOverUI)
            {
                if (t0.phase == TouchPhase.Began && IsTouchOverUI(t0.fingerId))
                {
                    _blockTouchFingerId = t0.fingerId;
                    return;
                }
                if (t1.phase == TouchPhase.Began && IsTouchOverUI(t1.fingerId))
                {
                    _blockTouchFingerId = t1.fingerId;
                    return;
                }
            }

            Vector2 p0Prev = t0.position - t0.deltaPosition;
            Vector2 p1Prev = t1.position - t1.deltaPosition;

            float prevDist = Vector2.Distance(p0Prev, p1Prev);
            float currDist = Vector2.Distance(t0.position, t1.position);
            float distDelta = currDist - prevDist;

            float size = cam.orthographicSize - distDelta * pinchZoomSpeed * cam.orthographicSize;
            cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);

            _touchPanning = false;
            return;
        }

        // 1 палец — пан
        Touch touch0 = Input.GetTouch(0);

        if (touch0.phase == TouchPhase.Began)
        {
            if (blockCameraWhenStartedOverUI && IsTouchOverUI(touch0.fingerId))
            {
                _blockTouchFingerId = touch0.fingerId;
                _touchPanning = false;
                return;
            }

            _lastTouchPos = touch0.position;
            _touchPanning = true;
        }
        else if (touch0.phase == TouchPhase.Moved && _touchPanning)
        {
            Vector2 cur = touch0.position;
            Vector2 delta = cur - _lastTouchPos;
            _lastTouchPos = cur;

            PanByScreenDelta(delta);
        }
        else if (touch0.phase == TouchPhase.Ended || touch0.phase == TouchPhase.Canceled)
        {
            _touchPanning = false;
        }
    }

    bool IsTouchOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    void PanByScreenDelta(Vector2 screenDelta)
    {
        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;

        float moveX = -screenDelta.x * worldPerPixel * dragSpeed;
        float moveZ = -screenDelta.y * worldPerPixel * dragSpeed;

        Vector3 pos = transform.position + new Vector3(moveX, 0f, moveZ);

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        transform.position = pos;
    }
}
