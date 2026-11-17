using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PlayButtonWiggle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool animate = true;
    public bool triplePulse;
    private bool pressed;
    private float pressAge;
    private float pressStartScale = 1;
    [Min(0f)] public float pauseDuration = 2f;
    [Min(0.1f)] public float shakeDuration = 0.6f;
    [Range(0f, 12f)] public float angle = 5f;
    [Range(0f, 0.15f)] public float scalePulse = 0.04f;

    private Quaternion restingRotation;
    private Vector3 restingScale;
    private float elapsed;
    private bool hasRestingTransform;
    private bool ownsTransform;
    public bool IsShaking { get; private set; }

    void OnEnable()
    {
        restingRotation = transform.localRotation;
        restingScale = transform.localScale;
        hasRestingTransform = true;
        elapsed = 0;
        pressed = false;
        ownsTransform = false;
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (triplePulse)
        {
            float pulseDuration=Mathf.Max(.01f,shakeDuration),cycle=elapsed%(pulseDuration+pauseDuration);
            float segment=Mathf.Clamp01(cycle/pulseDuration)*6;
            float triangle=1-Mathf.Abs(segment%2-1);
            bool pulsing=animate && cycle<pulseDuration;
            IsShaking=pulsing;
            if (!pulsing && !pressed) { RestoreTransform(); return; }
            CaptureTransform();
            float scale=pulsing ? 1+scalePulse*triangle : 1;
            if(pressed){pressAge+=Time.unscaledDeltaTime;scale=Mathf.Lerp(pressStartScale,.9f,Mathf.Clamp01(pressAge/.1f));}
            transform.localScale=restingScale*scale;
            return;
        }
        float duration = Mathf.Max(0.1f, shakeDuration);
        float pause = Mathf.Max(0, pauseDuration);
        float phase = elapsed % (pause + duration);
        IsShaking = animate && phase >= pause;
        if (!IsShaking)
        {
            RestoreTransform();
            return;
        }

        float progress = (phase - pause) / duration;
        CaptureTransform();
        float envelope = Mathf.Sin(progress * Mathf.PI);
        float rotation = Mathf.Sin(progress * Mathf.PI * 5f) * angle * envelope;
        transform.localRotation = restingRotation * Quaternion.Euler(0, 0, rotation);
        transform.localScale = restingScale * (1f + scalePulse * envelope);
    }

    void OnDisable()
    {
        IsShaking = false;
        RestoreTransform();
    }

    void RestoreTransform()
    {
        if (!hasRestingTransform || !ownsTransform) return;
        transform.localRotation = restingRotation;
        transform.localScale = restingScale;
        ownsTransform = false;
    }

    void CaptureTransform()
    {
        if (ownsTransform) return;
        restingRotation = transform.localRotation;
        restingScale = transform.localScale;
        hasRestingTransform = ownsTransform = true;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if(!triplePulse)return;
        CaptureTransform();
        pressed=true;pressAge=0;pressStartScale=transform.localScale.x/Mathf.Max(.001f,restingScale.x);
    }
    public void OnPointerUp(PointerEventData data) { pressed=false; }
    public void OnPointerExit(PointerEventData data) { pressed=false; }
}
