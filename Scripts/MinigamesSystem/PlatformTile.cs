using UnityEngine;

public enum PlatformTileState
{
    Normal,
    Start,
    Safe,
    Danger,
    Path,
    Finish,
    Warning,
    Decor,
    PlayerFail,
    Pendulum,
    Blocked,
    PowerSpeed
}

public class PlatformTile : MonoBehaviour
    {
        [Header("Grid")]
        public int GridX;
        public int GridY;

        [Header("Refs")]
        [SerializeField] private Renderer groundRenderer;

        [Header("Colors")]
        [SerializeField] private Color playerFailColor = Color.white;

        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color startColor = Color.blue;
        [SerializeField] private Color safeColor = Color.green;
        [SerializeField] private Color dangerColor = Color.red;
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private Color finishColor = Color.yellow;
        [SerializeField] private Color pendulumColor = new Color(0.02f, 0.02f, 0.02f);
        [SerializeField] private Color blockedColor = Color.black;

        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0f);
        [SerializeField] private Color decorColor = new Color(0.35f, 0.35f, 0.35f);
        [SerializeField] private Color powerSpeedColor = new Color(0.55f, 0f, 1f);

    public bool IsPlayerOnTile { get; private set; }

    public bool IsDanger =>
        CurrentState == PlatformTileState.Danger ||
        CurrentState == PlatformTileState.Pendulum ||
        CurrentState == PlatformTileState.Blocked;

    public bool IsSafe => CurrentState == PlatformTileState.Safe;
        public bool IsStart => CurrentState == PlatformTileState.Start;
        public bool IsFinish => CurrentState == PlatformTileState.Finish;
        public bool IsBlocked => CurrentState == PlatformTileState.Blocked;
    public PlatformTileState CurrentState { get; private set; }

        private bool stateInitialized;
        private MaterialPropertyBlock mpb;

        private void Awake()
    {
        if (!groundRenderer)
            groundRenderer = GetComponentInChildren<Renderer>();

        mpb = new MaterialPropertyBlock();
        SetNormal();
    }

    private void OnEnable()
    {
        PlatformMiniGameGrid.Register(this);
    }

    private void OnDisable()
    {
        PlatformMiniGameGrid.Unregister(this);
    }

    private void SetState(PlatformTileState state, Color color)
    {
        if (stateInitialized && CurrentState == state)
            return;

        CurrentState = state;
        stateInitialized = true;

        SetColor(color);
    }

    public void SetNormal()
    {
        SetState(PlatformTileState.Normal, normalColor);
    }

    public void SetStart()
    {
        SetState(PlatformTileState.Start, startColor);
    }

    public void SetSafe()
    {
        SetState(PlatformTileState.Safe, safeColor);
    }

    public void SetDanger()
    {
        SetState(PlatformTileState.Danger, dangerColor);
    }

    public void SetPath()
    {
        SetState(PlatformTileState.Path, pathColor);
    }

    public void SetFinish()
    {
        SetState(PlatformTileState.Finish, finishColor);
    }

    public void SetWarning()
    {
        SetState(PlatformTileState.Warning, warningColor);
    }

    public void SetDecor()
    {
        SetState(PlatformTileState.Decor, decorColor);
    }

    public void SetPlayerFail()
    {
        SetState(PlatformTileState.PlayerFail, playerFailColor);
    }

    public void SetPendulum()
    {
        SetState(PlatformTileState.Pendulum, pendulumColor);
    }

    public void SetBlocked()
    {
        SetState(PlatformTileState.Blocked, blockedColor);
    }

    public void SetPowerSpeed()
    {
        SetState(PlatformTileState.PowerSpeed, powerSpeedColor);
    }

    private void SetColor(Color color)
    {
        if (!groundRenderer) return;

        groundRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        groundRenderer.SetPropertyBlock(mpb);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        IsPlayerOnTile = true;
        PlatformMiniGameManager.Instance?.OnPlayerEnteredTile(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        IsPlayerOnTile = false;
        PlatformMiniGameManager.Instance?.OnPlayerExitedTile(this);
    }

    public bool ContainsWorldXZ(Vector3 worldPosition, float padding = 0.03f)
    {
        if (!groundRenderer)
            return false;

        Bounds bounds = groundRenderer.bounds;

        return worldPosition.x >= bounds.min.x + padding &&
               worldPosition.x <= bounds.max.x - padding &&
               worldPosition.z >= bounds.min.z + padding &&
               worldPosition.z <= bounds.max.z - padding;
    }

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        if (!groundRenderer)
            groundRenderer = GetComponentInChildren<Renderer>();

        if (!groundRenderer)
        {
            bounds = default;
            return false;
        }

        bounds = groundRenderer.bounds;
        return true;
    }
}