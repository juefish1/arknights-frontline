using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class ExusiaiPreviewDriver : MonoBehaviour
{
    public enum Mode { Sequence, Idle, Jog, Shoot, JogAndShoot }
    public Mode mode;
    [SerializeField] private ExusiaiPresentation presentation;
    [SerializeField] private float jogSpeed = 2.4f;
    private Vector3 origin;
    private float clock, nextShot;
    private Mode previousMode;

    public void Configure(ExusiaiPresentation target, float speed)
    {
        presentation = target;
        jogSpeed = speed;
    }

    private void OnEnable()
    {
        if (!presentation) presentation = GetComponent<ExusiaiPresentation>();
        origin = transform.position;
        clock = nextShot = 0;
        previousMode = mode;
    }

    private void Update()
    {
        if (!presentation || Time.deltaTime <= 0) return;
        if (mode != previousMode)
        {
            clock = nextShot = 0;
            presentation.ResetPresentation();
            transform.position = origin;
            previousMode = mode;
        }
        clock += Time.deltaTime;
        float t = clock % 18;
        if (mode == Mode.Sequence && t < Time.deltaTime && clock > 1)
        {
            transform.position = origin;
            presentation.ResetPresentation();
        }
        bool moving = mode == Mode.Jog || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 3 && t < 6) || (t >= 13 && t < 16)));
        bool shooting = mode == Mode.Shoot || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 7 && t < 10) || (t >= 13 && t < 16)));
        Vector3 direction = mode == Mode.Sequence && t >= 10 ? Vector3.back : Vector3.forward;
        bool prepared = mode == Mode.Shoot || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 6.65f && t < 10) || (t >= 12.65f && t < 16)));
        presentation.SetAiming(prepared);
        presentation.SetMoveSpeed(moving ? jogSpeed : 0);
        presentation.SetAimDirection(direction);
        // The camera previews a treadmill: movement facts drive animation; the user can
        // translate the actor to inspect secondary motion without the actor leaving view.
        if (shooting && clock >= 0.25f && clock >= nextShot)
        {
            presentation.PlayShot();
            nextShot = clock + 0.12f;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 210, 190), GUI.skin.box);
        GUILayout.Label("Exusiai animation preview");
        foreach (Mode value in System.Enum.GetValues(typeof(Mode)))
            if (GUILayout.Button(value.ToString())) mode = value;
        GUILayout.EndArea();
    }
}
